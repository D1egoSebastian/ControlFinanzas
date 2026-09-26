using System.Text;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Notifications;

/// <summary>
/// Aviso persistido de la Fase 17 (F-017, R-1): una fila por instancia,
/// reconciliada en cada corrida de detección, siguiendo el patrón de
/// <c>ControlDeFinanzasIA.Domain.Alerts.AnomalyAlert</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Solo hechos, nunca texto (R-11):</b> esta entidad guarda únicamente
/// las columnas de hechos (kind, subject, fechas, montos, etc.). El texto
/// visible se arma en la UI a partir de <c>Strings.resx</c>, en el momento
/// de mostrarlo.
/// </para>
/// <para>
/// <b>Un solo camino de constructor validado, cinco factories por tipo
/// (D-1):</b> <see cref="ForDebtInstallment"/>, <see cref="ForRecurringCharge"/>,
/// <see cref="ForBudget"/>, <see cref="ForAnomaly"/> y <see cref="ForDigest"/>
/// exponen únicamente los hechos que su tipo requiere; los hechos de los
/// demás tipos quedan sin establecer (<see langword="null"/> o vacíos).
/// </para>
/// <para>
/// <b><see cref="RefreshFacts"/> nunca toca <see cref="Id"/>,
/// <see cref="CreatedAt"/> ni <see cref="ReadAt"/>:</b> una fila
/// descartada (<see cref="IsDismissed"/>) nunca se recrea ni se
/// modifica — refrescar una fila descartada lanza
/// <see cref="InvalidOperationException"/>, igual que
/// <c>AnomalyAlert.Refresh</c>.
/// </para>
/// </remarks>
public sealed class Notification
{
    /// <summary>Longitud máxima de <see cref="SubjectLabel"/> después de sanitizar.</summary>
    public const int MaxLabelLength = 120;

    private readonly List<NotificationCurrencyTotal> _digestTotals = [];

    /// <summary>
    /// Constructor sin parámetros reservado para EF Core. No debe usarse
    /// desde código de aplicación.
    /// </summary>
    private Notification()
    {
    }

    private Notification(
        Guid id,
        NotificationKind kind,
        string dedupKey,
        Guid? sourceId,
        DateOnly referenceDate,
        string subjectLabel,
        decimal? amount,
        Currency? currency,
        decimal? percent,
        NotificationTier? tier,
        DigestFrequency? digestFrequency,
        int? digestPaymentCount,
        int? digestBudgetCount,
        int? digestAnomalyCount,
        IReadOnlyList<NotificationCurrencyTotal> digestTotals,
        DateTime createdAtUtc)
    {
        Id = id;
        Kind = kind;
        DedupKey = dedupKey;
        SourceId = sourceId;
        ReferenceDate = referenceDate;
        SubjectLabel = subjectLabel;
        Amount = amount;
        Currency = currency;
        Percent = percent;
        Tier = tier;
        DigestFrequency = digestFrequency;
        DigestPaymentCount = digestPaymentCount;
        DigestBudgetCount = digestBudgetCount;
        DigestAnomalyCount = digestAnomalyCount;
        _digestTotals.AddRange(digestTotals);
        CreatedAt = createdAtUtc;
    }

    /// <summary>Identificador único del aviso.</summary>
    public Guid Id { get; private set; }

    /// <summary>Tipo de aviso (F-017, R-2).</summary>
    public NotificationKind Kind { get; private set; }

    /// <summary>
    /// Clave de idempotencia única (D-2), calculada por
    /// <see cref="NotificationKeys"/> — nunca provista por el caller.
    /// </summary>
    public string DedupKey { get; private set; } = string.Empty;

    /// <summary>
    /// Id de la cuota, regla, presupuesto o alerta de origen.
    /// <see langword="null"/> para <see cref="NotificationKind.Digest"/>.
    /// </summary>
    public Guid? SourceId { get; private set; }

    /// <summary>
    /// Fecha de referencia: vencimiento, próxima generación, inicio del
    /// mes del presupuesto, fecha de la alerta, o inicio del período del
    /// resumen. Conduce la ventana de purga y el orden del flyout.
    /// </summary>
    public DateOnly ReferenceDate { get; private set; }

    /// <summary>Snapshot sanitizado del sujeto del aviso. Puede ser vacío.</summary>
    public string SubjectLabel { get; private set; } = string.Empty;

    /// <summary>Monto (solo cuotas/cargos). <see langword="null"/> para los demás tipos.</summary>
    public decimal? Amount { get; private set; }

    /// <summary>Moneda del <see cref="Amount"/>. <see langword="null"/> para los demás tipos.</summary>
    public Currency? Currency { get; private set; }

    /// <summary>Porcentaje consumido del presupuesto (solo <see cref="NotificationKind.BudgetThreshold"/>).</summary>
    public decimal? Percent { get; private set; }

    /// <summary>Nivel alcanzado (solo <see cref="NotificationKind.BudgetThreshold"/>).</summary>
    public NotificationTier? Tier { get; private set; }

    /// <summary>Frecuencia del resumen (solo <see cref="NotificationKind.Digest"/>).</summary>
    public DigestFrequency? DigestFrequency { get; private set; }

    /// <summary>Cantidad de pagos pendientes en la ventana del resumen (solo Digest).</summary>
    public int? DigestPaymentCount { get; private set; }

    /// <summary>Cantidad de presupuestos en <c>Near</c> o <c>Exceeded</c> (solo Digest).</summary>
    public int? DigestBudgetCount { get; private set; }

    /// <summary>Cantidad de anomalías sin reconocer del mes actual (solo Digest).</summary>
    public int? DigestAnomalyCount { get; private set; }

    /// <summary>
    /// Totales por moneda de los pagos pendientes en la ventana del
    /// resumen (solo Digest). Lista vacía para los demás tipos.
    /// </summary>
    public IReadOnlyList<NotificationCurrencyTotal> DigestTotals => _digestTotals;

    /// <summary>Momento UTC en que se creó el aviso. Estable a través de <see cref="RefreshFacts"/>.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Momento UTC en que se marcó como leído, o <see langword="null"/>.</summary>
    public DateTime? ReadAt { get; private set; }

    /// <summary>Momento UTC en que el usuario descartó el aviso, o <see langword="null"/>.</summary>
    public DateTime? DismissedAt { get; private set; }

    /// <summary><see langword="true"/> si el aviso fue marcado como leído.</summary>
    public bool IsRead => ReadAt is not null;

    /// <summary><see langword="true"/> si el aviso fue descartado por el usuario.</summary>
    public bool IsDismissed => DismissedAt is not null;

    /// <summary>
    /// Crea un aviso <see cref="NotificationKind.DebtInstallmentDue"/>
    /// (R-4).
    /// </summary>
    /// <param name="paymentId">Id del <c>DebtPayment</c> marcado (no vacío).</param>
    /// <param name="dueDate">Fecha de vencimiento de la cuota.</param>
    /// <param name="label">Etiqueta del sujeto (se sanitiza; puede quedar vacía).</param>
    /// <param name="amount">Monto de la cuota (requerido).</param>
    /// <param name="createdAtUtc">Momento UTC de creación (no puede ser <c>default</c>).</param>
    /// <exception cref="ArgumentException">Si <paramref name="paymentId"/> está vacío o <paramref name="createdAtUtc"/> es <c>default</c>.</exception>
    /// <exception cref="ArgumentNullException">Si <paramref name="amount"/> es <see langword="null"/>.</exception>
    public static Notification ForDebtInstallment(
        Guid paymentId, DateOnly dueDate, string label, Money amount, DateTime createdAtUtc)
    {
        ValidateSourceId(paymentId, nameof(paymentId));
        ArgumentNullException.ThrowIfNull(amount);
        ValidateCreatedAt(createdAtUtc);

        return new Notification(
            Guid.NewGuid(),
            NotificationKind.DebtInstallmentDue,
            NotificationKeys.ForDebtInstallment(paymentId),
            paymentId,
            dueDate,
            SanitizeLabel(label),
            amount.Amount,
            amount.Currency,
            percent: null,
            tier: null,
            digestFrequency: null,
            digestPaymentCount: null,
            digestBudgetCount: null,
            digestAnomalyCount: null,
            digestTotals: [],
            createdAtUtc);
    }

    /// <summary>
    /// Crea un aviso <see cref="NotificationKind.RecurringChargeDue"/>
    /// (R-4). Cubre tanto suscripciones como gastos fijos a través de un
    /// único camino de lectura (proposal D7).
    /// </summary>
    /// <param name="ruleId">Id de la <c>RecurringRule</c> marcada (no vacío).</param>
    /// <param name="chargeDate"><c>NextGenerationDate</c> al momento de la detección.</param>
    /// <param name="label">Etiqueta del sujeto (se sanitiza; puede quedar vacía).</param>
    /// <param name="amount">Monto del cargo (requerido).</param>
    /// <param name="createdAtUtc">Momento UTC de creación (no puede ser <c>default</c>).</param>
    /// <exception cref="ArgumentException">Si <paramref name="ruleId"/> está vacío o <paramref name="createdAtUtc"/> es <c>default</c>.</exception>
    /// <exception cref="ArgumentNullException">Si <paramref name="amount"/> es <see langword="null"/>.</exception>
    public static Notification ForRecurringCharge(
        Guid ruleId, DateOnly chargeDate, string label, Money amount, DateTime createdAtUtc)
    {
        ValidateSourceId(ruleId, nameof(ruleId));
        ArgumentNullException.ThrowIfNull(amount);
        ValidateCreatedAt(createdAtUtc);

        return new Notification(
            Guid.NewGuid(),
            NotificationKind.RecurringChargeDue,
            NotificationKeys.ForRecurringCharge(ruleId, chargeDate),
            ruleId,
            chargeDate,
            SanitizeLabel(label),
            amount.Amount,
            amount.Currency,
            percent: null,
            tier: null,
            digestFrequency: null,
            digestPaymentCount: null,
            digestBudgetCount: null,
            digestAnomalyCount: null,
            digestTotals: [],
            createdAtUtc);
    }

    /// <summary>
    /// Crea un aviso <see cref="NotificationKind.BudgetThreshold"/> (R-5).
    /// </summary>
    /// <param name="budgetId">Id del <c>Budget</c> marcado (no vacío).</param>
    /// <param name="monthStart">Primer día del mes evaluado.</param>
    /// <param name="label">Etiqueta del sujeto (se sanitiza; puede quedar vacía).</param>
    /// <param name="percent">Porcentaje consumido (<c>BudgetEvaluation.PercentConsumed</c>).</param>
    /// <param name="tier">Nivel alcanzado (debe ser un valor definido de <see cref="NotificationTier"/>).</param>
    /// <param name="createdAtUtc">Momento UTC de creación (no puede ser <c>default</c>).</param>
    /// <exception cref="ArgumentException">
    /// Si <paramref name="budgetId"/> está vacío, <paramref name="tier"/> no es un valor
    /// definido, o <paramref name="createdAtUtc"/> es <c>default</c>.
    /// </exception>
    public static Notification ForBudget(
        Guid budgetId, DateOnly monthStart, string label, decimal percent, NotificationTier tier, DateTime createdAtUtc)
    {
        ValidateSourceId(budgetId, nameof(budgetId));
        ValidateTier(tier);
        ValidateCreatedAt(createdAtUtc);

        return new Notification(
            Guid.NewGuid(),
            NotificationKind.BudgetThreshold,
            NotificationKeys.ForBudget(budgetId, monthStart, tier),
            budgetId,
            monthStart,
            SanitizeLabel(label),
            amount: null,
            currency: null,
            percent,
            tier,
            digestFrequency: null,
            digestPaymentCount: null,
            digestBudgetCount: null,
            digestAnomalyCount: null,
            digestTotals: [],
            createdAtUtc);
    }

    /// <summary>
    /// Crea un aviso <see cref="NotificationKind.Anomaly"/> (R-6).
    /// </summary>
    /// <param name="alertId">Id de la <c>AnomalyAlert</c> (F-014) marcada (no vacío).</param>
    /// <param name="alertDate">Fecha del gasto marcado por la alerta.</param>
    /// <param name="label">Etiqueta del sujeto (se sanitiza; puede quedar vacía).</param>
    /// <param name="createdAtUtc">Momento UTC de creación (no puede ser <c>default</c>).</param>
    /// <exception cref="ArgumentException">Si <paramref name="alertId"/> está vacío o <paramref name="createdAtUtc"/> es <c>default</c>.</exception>
    public static Notification ForAnomaly(Guid alertId, DateOnly alertDate, string label, DateTime createdAtUtc)
    {
        ValidateSourceId(alertId, nameof(alertId));
        ValidateCreatedAt(createdAtUtc);

        return new Notification(
            Guid.NewGuid(),
            NotificationKind.Anomaly,
            NotificationKeys.ForAnomaly(alertId),
            alertId,
            alertDate,
            SanitizeLabel(label),
            amount: null,
            currency: null,
            percent: null,
            tier: null,
            digestFrequency: null,
            digestPaymentCount: null,
            digestBudgetCount: null,
            digestAnomalyCount: null,
            digestTotals: [],
            createdAtUtc);
    }

    /// <summary>
    /// Crea un aviso <see cref="NotificationKind.Digest"/> (R-13). Sin
    /// <see cref="SourceId"/>: no hay una fila de origen única.
    /// </summary>
    /// <param name="frequency">Frecuencia configurada (no puede ser <see cref="Notifications.DigestFrequency.Off"/>).</param>
    /// <param name="periodStart">Primer día del período cubierto por el resumen.</param>
    /// <param name="payments">Cantidad de pagos pendientes en la ventana (&gt;= 0).</param>
    /// <param name="totals">Totales por moneda de esos pagos (requerido; puede ser vacío).</param>
    /// <param name="budgets">Cantidad de presupuestos en <c>Near</c> o <c>Exceeded</c> (&gt;= 0).</param>
    /// <param name="anomalies">Cantidad de anomalías sin reconocer del mes actual (&gt;= 0).</param>
    /// <param name="createdAtUtc">Momento UTC de creación (no puede ser <c>default</c>).</param>
    /// <exception cref="ArgumentException">
    /// Si <paramref name="frequency"/> no es un valor definido o es <c>Off</c>, o si
    /// <paramref name="createdAtUtc"/> es <c>default</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <paramref name="payments"/>, <paramref name="budgets"/> o <paramref name="anomalies"/> es negativo.
    /// </exception>
    /// <exception cref="ArgumentNullException">Si <paramref name="totals"/> es <see langword="null"/>.</exception>
    public static Notification ForDigest(
        DigestFrequency frequency,
        DateOnly periodStart,
        int payments,
        IReadOnlyList<NotificationCurrencyTotal> totals,
        int budgets,
        int anomalies,
        DateTime createdAtUtc)
    {
        ValidateDigestFrequency(frequency);
        ArgumentNullException.ThrowIfNull(totals);
        ValidateNonNegative(payments, nameof(payments));
        ValidateNonNegative(budgets, nameof(budgets));
        ValidateNonNegative(anomalies, nameof(anomalies));
        ValidateCreatedAt(createdAtUtc);

        return new Notification(
            Guid.NewGuid(),
            NotificationKind.Digest,
            NotificationKeys.ForDigest(frequency, periodStart),
            sourceId: null,
            periodStart,
            subjectLabel: string.Empty,
            amount: null,
            currency: null,
            percent: null,
            tier: null,
            frequency,
            payments,
            budgets,
            anomalies,
            totals,
            createdAtUtc);
    }

    /// <summary>
    /// Marca el aviso como leído. Idempotente: una segunda llamada
    /// conserva el primer timestamp (R-8).
    /// </summary>
    /// <param name="utc">Momento UTC de la lectura.</param>
    public void MarkRead(DateTime utc)
    {
        if (IsRead)
        {
            return;
        }

        ReadAt = utc;
    }

    /// <summary>
    /// Descarta el aviso de forma permanente. Idempotente: una segunda
    /// llamada conserva el primer timestamp (R-8).
    /// </summary>
    /// <param name="utc">Momento UTC del descarte.</param>
    public void Dismiss(DateTime utc)
    {
        if (IsDismissed)
        {
            return;
        }

        DismissedAt = utc;
    }

    /// <summary>
    /// Compara únicamente las columnas de hechos con <paramref name="other"/>
    /// — nunca <see cref="Id"/>, <see cref="CreatedAt"/>, <see cref="ReadAt"/>
    /// ni <see cref="DismissedAt"/>.
    /// </summary>
    /// <param name="other">El aviso contra el que comparar.</param>
    /// <returns><see langword="true"/> si todos los hechos coinciden.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="other"/> es <see langword="null"/>.</exception>
    public bool HasSameFacts(Notification other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Kind == other.Kind
            && SourceId == other.SourceId
            && ReferenceDate == other.ReferenceDate
            && SubjectLabel == other.SubjectLabel
            && Amount == other.Amount
            && Currency == other.Currency
            && Percent == other.Percent
            && Tier == other.Tier
            && DigestFrequency == other.DigestFrequency
            && DigestPaymentCount == other.DigestPaymentCount
            && DigestBudgetCount == other.DigestBudgetCount
            && DigestAnomalyCount == other.DigestAnomalyCount
            && _digestTotals.SequenceEqual(other._digestTotals);
    }

    /// <summary>
    /// Re-sincroniza los hechos de esta fila NO descartada con los de
    /// <paramref name="from"/> (un candidato recién construido por el
    /// mismo <c>DedupKey</c>), preservando <see cref="Id"/>,
    /// <see cref="CreatedAt"/> y <see cref="ReadAt"/>.
    /// </summary>
    /// <param name="from">El candidato con los hechos vigentes.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="from"/> es <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Si este aviso ya fue descartado (<see cref="IsDismissed"/>).</exception>
    public void RefreshFacts(Notification from)
    {
        ArgumentNullException.ThrowIfNull(from);

        if (IsDismissed)
        {
            throw new InvalidOperationException(
                $"Notification {Id} is already dismissed and cannot be refreshed.");
        }

        Kind = from.Kind;
        DedupKey = from.DedupKey;
        SourceId = from.SourceId;
        ReferenceDate = from.ReferenceDate;
        SubjectLabel = from.SubjectLabel;
        Amount = from.Amount;
        Currency = from.Currency;
        Percent = from.Percent;
        Tier = from.Tier;
        DigestFrequency = from.DigestFrequency;
        DigestPaymentCount = from.DigestPaymentCount;
        DigestBudgetCount = from.DigestBudgetCount;
        DigestAnomalyCount = from.DigestAnomalyCount;
        _digestTotals.Clear();
        _digestTotals.AddRange(from._digestTotals);
    }

    /// <summary>
    /// Sanitiza una etiqueta de sujeto (R-11): reemplaza caracteres de
    /// control por espacio, colapsa espacios en blanco consecutivos,
    /// recorta los extremos y corta a <see cref="MaxLabelLength"/>
    /// caracteres.
    /// </summary>
    private static string SanitizeLabel(string? label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(label.Length);
        bool lastAppendedWasSpace = false;

        foreach (char c in label)
        {
            char normalized = char.IsControl(c) ? ' ' : c;

            if (char.IsWhiteSpace(normalized))
            {
                if (!lastAppendedWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    lastAppendedWasSpace = true;
                }
            }
            else
            {
                builder.Append(normalized);
                lastAppendedWasSpace = false;
            }
        }

        string collapsed = builder.ToString().TrimEnd(' ');

        return collapsed.Length > MaxLabelLength ? collapsed[..MaxLabelLength] : collapsed;
    }

    private static void ValidateSourceId(Guid sourceId, string paramName)
    {
        if (sourceId == Guid.Empty)
        {
            throw new ArgumentException("Source id is required.", paramName);
        }
    }

    private static void ValidateTier(NotificationTier tier)
    {
        if (!Enum.IsDefined(tier))
        {
            throw new ArgumentException($"Tier value '{tier}' is not defined.", nameof(tier));
        }
    }

    private static void ValidateDigestFrequency(DigestFrequency frequency)
    {
        if (!Enum.IsDefined(frequency))
        {
            throw new ArgumentException($"DigestFrequency value '{frequency}' is not defined.", nameof(frequency));
        }

        if (frequency == Notifications.DigestFrequency.Off)
        {
            throw new ArgumentException("DigestFrequency must not be Off for a digest notification.", nameof(frequency));
        }
    }

    private static void ValidateNonNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be non-negative.");
        }
    }

    private static void ValidateCreatedAt(DateTime createdAtUtc)
    {
        if (createdAtUtc == default)
        {
            throw new ArgumentException("CreatedAt is required.", nameof(createdAtUtc));
        }
    }
}
