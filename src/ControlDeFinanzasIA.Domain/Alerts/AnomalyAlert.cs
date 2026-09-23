namespace ControlDeFinanzasIA.Domain.Alerts;

/// <summary>
/// Alerta de anomalía detectada por <see cref="AnomalyDetector"/> (F-014,
/// R-3). Persistida de forma idempotente (índice único sobre
/// <see cref="TransactionId"/>) y reconciliada en cada carga del
/// dashboard: una alerta reconocida (<see cref="IsAcknowledged"/>) nunca
/// se modifica ni se vuelve a crear.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sin FK a <c>Expense</c> (D-8):</b> la alerta sobrevive al borrado
/// del gasto que la originó hasta la siguiente reconciliación de su mes,
/// y una alerta reconocida sobrevive para siempre.
/// </para>
/// <para>
/// <b>Patron de persistencia:</b> setters privados y constructor sin
/// parametros para EF Core, factory estatico <see cref="Create"/> que
/// valida todas las invariantes antes de construir. Mismo patron que
/// <c>Domain.Goals.Goal</c> y <c>Domain.Debts.Debt</c>.
/// </para>
/// <para>
/// <b><see cref="Refresh"/> preserva el <see cref="Id"/> estable:</b>
/// cuando el crecimiento del historial cambia la severidad de una
/// anomalía no reconocida, la Fase 17 (notificaciones) necesita algo
/// estable sobre lo cual anclar "ya notificado" — un <see cref="Id"/>
/// que cambiara en cada reconciliación forzaría un re-aviso espurio.
/// </para>
/// </remarks>
public sealed class AnomalyAlert
{
    /// <summary>Longitud máxima de <see cref="Message"/> después de <c>Trim()</c>.</summary>
    public const int MaxMessageLength = 500;

    /// <summary>
    /// Constructor sin parametros reservado para EF Core. No debe
    /// usarse desde codigo de aplicacion.
    /// </summary>
    private AnomalyAlert()
    {
    }

    private AnomalyAlert(
        Guid id,
        Guid transactionId,
        Guid categoryId,
        DateOnly date,
        AnomalySeverity severity,
        string message,
        DateTime detectedAtUtc)
    {
        Id = id;
        TransactionId = transactionId;
        CategoryId = categoryId;
        Date = date;
        Severity = severity;
        Message = message;
        DetectedAt = detectedAtUtc;
    }

    /// <summary>Identificador único de la alerta.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Id del <c>Expense</c> marcado. Sin FK (D-8): la alerta sobrevive
    /// al borrado del gasto hasta la siguiente reconciliación de su mes,
    /// y una alerta reconocida sobrevive para siempre.
    /// </summary>
    public Guid TransactionId { get; private set; }

    /// <summary>Categoría del gasto marcado al momento de la detección.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>
    /// Fecha del gasto al momento de la detección. Clave de alcance
    /// mensual para la reconciliación.
    /// </summary>
    public DateOnly Date { get; private set; }

    /// <summary>Severidad de la anomalía.</summary>
    public AnomalySeverity Severity { get; private set; }

    /// <summary>
    /// Texto plantilla determinístico (D-9). Nunca contiene texto
    /// generado por un LLM.
    /// </summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>Momento UTC en que se detectó la anomalía.</summary>
    public DateTime DetectedAt { get; private set; }

    /// <summary>Momento UTC en que el usuario reconoció (descartó) la alerta, o <see langword="null"/>.</summary>
    public DateTime? AcknowledgedAt { get; private set; }

    /// <summary><see langword="true"/> si la alerta fue reconocida por el usuario.</summary>
    public bool IsAcknowledged => AcknowledgedAt is not null;

    /// <summary>
    /// Crea una nueva alerta validando todas las invariantes (R-3).
    /// </summary>
    /// <param name="transactionId">Id del <c>Expense</c> marcado (no vacío).</param>
    /// <param name="categoryId">Categoría del gasto marcado (no vacío).</param>
    /// <param name="date">Fecha del gasto al momento de la detección.</param>
    /// <param name="severity">Severidad de la anomalía (debe ser un valor definido de <see cref="AnomalySeverity"/>).</param>
    /// <param name="message">Texto plantilla determinístico (no vacío, máximo <see cref="MaxMessageLength"/> chars tras <c>Trim()</c>).</param>
    /// <param name="detectedAtUtc">Momento UTC de la detección (no puede ser <c>default</c>).</param>
    /// <returns>Una instancia válida de <see cref="AnomalyAlert"/> no reconocida.</returns>
    /// <exception cref="ArgumentException">Si se viola alguna invariante.</exception>
    public static AnomalyAlert Create(
        Guid transactionId,
        Guid categoryId,
        DateOnly date,
        AnomalySeverity severity,
        string message,
        DateTime detectedAtUtc)
    {
        ValidateTransactionId(transactionId);
        ValidateCategoryId(categoryId);
        ValidateSeverity(severity);
        string normalizedMessage = ValidateMessage(message);
        ValidateDetectedAt(detectedAtUtc);

        return new AnomalyAlert(
            Guid.NewGuid(), transactionId, categoryId, date, severity, normalizedMessage, detectedAtUtc);
    }

    /// <summary>
    /// Re-sincroniza una alerta NO reconocida con la salida actual del
    /// detector, preservando su <see cref="Id"/> estable.
    /// </summary>
    /// <param name="date">Nueva fecha del gasto.</param>
    /// <param name="severity">Nueva severidad.</param>
    /// <param name="message">Nuevo texto plantilla.</param>
    /// <exception cref="InvalidOperationException">Si la alerta ya fue reconocida (regla de descarte, D-8).</exception>
    /// <exception cref="ArgumentException">Si <paramref name="severity"/> o <paramref name="message"/> violan una invariante.</exception>
    public void Refresh(DateOnly date, AnomalySeverity severity, string message)
    {
        if (IsAcknowledged)
        {
            throw new InvalidOperationException(
                $"AnomalyAlert {Id} is already acknowledged and cannot be refreshed.");
        }

        ValidateSeverity(severity);
        string normalizedMessage = ValidateMessage(message);

        Date = date;
        Severity = severity;
        Message = normalizedMessage;
    }

    /// <summary>
    /// Reconoce (descarta) la alerta. Idempotente: una segunda llamada
    /// conserva el primer timestamp.
    /// </summary>
    /// <param name="acknowledgedAtUtc">Momento UTC del reconocimiento.</param>
    public void Acknowledge(DateTime acknowledgedAtUtc)
    {
        if (IsAcknowledged)
        {
            return;
        }

        AcknowledgedAt = acknowledgedAtUtc;
    }

    private static void ValidateTransactionId(Guid transactionId)
    {
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException("TransactionId is required.", nameof(transactionId));
        }
    }

    private static void ValidateCategoryId(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("CategoryId is required.", nameof(categoryId));
        }
    }

    private static void ValidateSeverity(AnomalySeverity severity)
    {
        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentException($"Severity value '{severity}' is not defined.", nameof(severity));
        }
    }

    private static string ValidateMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message is required.", nameof(message));
        }

        string trimmed = message.Trim();
        if (trimmed.Length > MaxMessageLength)
        {
            throw new ArgumentException(
                $"Message must be at most {MaxMessageLength} characters. Received: {trimmed.Length}.",
                nameof(message));
        }

        return trimmed;
    }

    private static void ValidateDetectedAt(DateTime detectedAtUtc)
    {
        if (detectedAtUtc == default)
        {
            throw new ArgumentException("DetectedAt is required.", nameof(detectedAtUtc));
        }
    }
}
