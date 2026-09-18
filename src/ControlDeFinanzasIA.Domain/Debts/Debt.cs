using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Debts;

/// <summary>
/// Raiz de agregado que modela una deuda con un plan de cuotas
/// completo (F-001 de Fase 7). A diferencia de
/// <c>Domain.Recurring.RecurringRule</c>, el numero de ocurrencias es
/// finito y conocido desde la creacion: no hay "proxima generacion"
/// indefinida, el calendario completo de <see cref="DebtPayment"/> se
/// genera y persiste una sola vez (R-3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Invariantes (R-1, R-2):</b>
/// <list type="bullet">
///   <item>Description no vacia (max <see cref="MaxDescriptionLength"/> chars).</item>
///   <item>OriginalAmount y InstallmentAmount positivos (&gt; 0).</item>
///   <item>TotalInstallments &gt; 0.</item>
///   <item>Frequency es uno de los valores validos de <see cref="DebtFrequency"/>.</item>
///   <item>StartDate &gt;= 1900-01-01.</item>
///   <item>CounterpartyName y Note son opcionales (longitud maxima acotada si se proveen).</item>
/// </list>
/// </para>
/// <para>
/// <b>Patron de persistencia:</b> setters privados y constructor sin
/// parametros para EF Core, factory estatico <see cref="Create"/> que
/// valida todas las invariantes antes de construir. Mismo patron que
/// <c>RecurringRule</c> (Fase 6).
/// </para>
/// <para>
/// El calculo del saldo pendiente (R-7) y el cierre automatico (R-6)
/// viven en el dominio (<see cref="CalculatePendingBalance"/>,
/// <see cref="TryAutoClose"/>) para que sean verificables sin
/// recalcular la logica fuera de esta clase.
/// </para>
/// </remarks>
public sealed class Debt
{
    /// <summary>Longitud maxima de la descripcion.</summary>
    public const int MaxDescriptionLength = 200;

    /// <summary>Longitud maxima del nombre de la contraparte.</summary>
    public const int MaxCounterpartyNameLength = 100;

    /// <summary>Longitud maxima de la nota.</summary>
    public const int MaxNoteLength = 500;

    /// <summary>Identificador unico de la deuda.</summary>
    public Guid Id { get; private set; }

    /// <summary>Descripcion de la deuda (ej. "Prestamo carro", "Tarjeta de credito").</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Monto original de la deuda (positivo).</summary>
    public Money OriginalAmount { get; private set; } = null!;

    /// <summary>Moneda de la deuda (denormalizado para queries, igual que OriginalAmount.Currency).</summary>
    public Currency Currency { get; private set; }

    /// <summary>Numero total de cuotas del plan (entero positivo).</summary>
    public int TotalInstallments { get; private set; }

    /// <summary>Monto de cada cuota (positivo).</summary>
    public Money InstallmentAmount { get; private set; } = null!;

    /// <summary>Frecuencia de las cuotas: <c>"Monthly"</c> o <c>"Biweekly"</c>.</summary>
    public string Frequency { get; private set; } = nameof(DebtFrequency.Monthly);

    /// <summary>Fecha de inicio del plan (y fecha de la primera cuota).</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>Nombre de la contraparte (a quien se le debe). Opcional.</summary>
    public string? CounterpartyName { get; private set; }

    /// <summary>Nota libre opcional.</summary>
    public string? Note { get; private set; }

    /// <summary>Estado actual de la deuda.</summary>
    public DebtStatus Status { get; private set; } = DebtStatus.Active;

    /// <summary>
    /// Constructor sin parametros reservado para EF Core. No debe
    /// usarse desde codigo de aplicacion.
    /// </summary>
    private Debt()
    {
    }

    /// <summary>
    /// Constructor con parametros para uso interno del factory
    /// <see cref="Create"/>. El caller ya valido las invariantes.
    /// </summary>
    private Debt(
        Guid id,
        string description,
        Money originalAmount,
        Currency currency,
        int totalInstallments,
        Money installmentAmount,
        string frequency,
        DateOnly startDate,
        string? counterpartyName,
        string? note,
        DebtStatus status)
    {
        Id = id;
        Description = description;
        OriginalAmount = originalAmount;
        Currency = currency;
        TotalInstallments = totalInstallments;
        InstallmentAmount = installmentAmount;
        Frequency = frequency;
        StartDate = startDate;
        CounterpartyName = counterpartyName;
        Note = note;
        Status = status;
    }

    /// <summary>
    /// Crea una nueva deuda validando todas las invariantes (R-1, R-2).
    /// No genera ni persiste el calendario de cuotas: eso es
    /// responsabilidad de <see cref="DebtScheduleGenerator"/> y del
    /// use case orquestador.
    /// </summary>
    /// <param name="description">Descripcion de la deuda (1..200 chars).</param>
    /// <param name="originalAmount">Monto original de la deuda (&gt; 0).</param>
    /// <param name="currency">Moneda de la deuda.</param>
    /// <param name="totalInstallments">Numero total de cuotas (&gt; 0).</param>
    /// <param name="installmentAmount">Monto de cada cuota (&gt; 0).</param>
    /// <param name="frequency">Frecuencia: <c>"Monthly"</c> o <c>"Biweekly"</c>.</param>
    /// <param name="startDate">Fecha de inicio (y primera cuota).</param>
    /// <param name="counterpartyName">Contraparte opcional (max 100 chars).</param>
    /// <param name="note">Nota opcional (max 500 chars).</param>
    /// <returns>Una instancia valida de <see cref="Debt"/> en estado Active.</returns>
    /// <exception cref="ArgumentException">Si se viola alguna invariante.</exception>
    public static Debt Create(
        string description,
        Money originalAmount,
        Currency currency,
        int totalInstallments,
        Money installmentAmount,
        string frequency,
        DateOnly startDate,
        string? counterpartyName = null,
        string? note = null)
    {
        ArgumentNullException.ThrowIfNull(originalAmount);
        ArgumentNullException.ThrowIfNull(installmentAmount);

        string normalizedDescription = ValidateDescription(description);
        string normalizedFrequency = ValidateFrequency(frequency);
        ValidateStartDate(startDate);
        ValidateTotalInstallments(totalInstallments);
        string? normalizedCounterparty = ValidateOptionalLength(
            counterpartyName, MaxCounterpartyNameLength, nameof(counterpartyName));
        string? normalizedNote = ValidateOptionalLength(note, MaxNoteLength, nameof(note));

        if (originalAmount.Amount <= 0)
        {
            throw new ArgumentException(
                $"OriginalAmount must be strictly positive. Received: {originalAmount.Amount}.",
                nameof(originalAmount));
        }

        if (installmentAmount.Amount <= 0)
        {
            throw new ArgumentException(
                $"InstallmentAmount must be strictly positive. Received: {installmentAmount.Amount}.",
                nameof(installmentAmount));
        }

        return new Debt(
            Guid.NewGuid(),
            normalizedDescription,
            originalAmount,
            currency,
            totalInstallments,
            installmentAmount,
            normalizedFrequency,
            startDate,
            normalizedCounterparty,
            normalizedNote,
            DebtStatus.Active);
    }

    /// <summary>
    /// Calcula el saldo pendiente de la deuda (R-7):
    /// <c>OriginalAmount - Sum(payments donde Status = Paid)</c>.
    /// Vive en el dominio para que sea verificable sin recalcular la
    /// logica fuera de esta clase.
    /// </summary>
    /// <param name="payments">
    /// Todas las <see cref="DebtPayment"/> del plan de esta deuda (no
    /// solo las pagadas: el metodo filtra internamente).
    /// </param>
    /// <returns>Un <see cref="Money"/> con el saldo pendiente, en la moneda de la deuda.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payments"/> es null.</exception>
    public Money CalculatePendingBalance(IReadOnlyList<DebtPayment> payments)
    {
        ArgumentNullException.ThrowIfNull(payments);

        Money paidTotal = payments
            .Where(p => p.Status == DebtPaymentStatus.Paid)
            .Aggregate(Money.Zero(Currency), (total, payment) => total.Add(payment.Amount));

        return OriginalAmount.Subtract(paidTotal);
    }

    /// <summary>
    /// Evalua el cierre automatico de la deuda (R-6): si todas las
    /// cuotas del plan estan en estado Paid o Skipped, y la de mayor
    /// <see cref="DebtPayment.InstallmentNumber"/> (la ultima) esta en
    /// Paid, la deuda pasa a <see cref="DebtStatus.Closed"/>.
    /// </summary>
    /// <param name="payments">Todas las <see cref="DebtPayment"/> del plan de esta deuda.</param>
    /// <returns>
    /// <see langword="true"/> si la deuda se cerro como resultado de
    /// esta llamada; <see langword="false"/> si ya estaba cerrada o si
    /// la condicion de cierre no se cumple todavia.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="payments"/> es null.</exception>
    public bool TryAutoClose(IReadOnlyList<DebtPayment> payments)
    {
        ArgumentNullException.ThrowIfNull(payments);

        if (Status == DebtStatus.Closed || payments.Count == 0)
        {
            return false;
        }

        if (payments.Any(p => p.Status == DebtPaymentStatus.Pending))
        {
            return false;
        }

        DebtPayment lastInstallment = payments.OrderByDescending(p => p.InstallmentNumber).First();
        if (lastInstallment.Status != DebtPaymentStatus.Paid)
        {
            return false;
        }

        Status = DebtStatus.Closed;
        return true;
    }

    private static string ValidateDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        string trimmed = description.Trim();
        if (trimmed.Length > MaxDescriptionLength)
        {
            throw new ArgumentException(
                $"Description must be at most {MaxDescriptionLength} characters. Received: {trimmed.Length}.",
                nameof(description));
        }

        return trimmed;
    }

    private static string ValidateFrequency(string? frequency)
    {
        if (string.IsNullOrWhiteSpace(frequency))
        {
            throw new ArgumentException("Frequency is required.", nameof(frequency));
        }

        bool valid = frequency is nameof(DebtFrequency.Monthly) or nameof(DebtFrequency.Biweekly);
        if (!valid)
        {
            throw new ArgumentException(
                $"Frequency must be one of: Monthly, Biweekly. Received: {frequency}.",
                nameof(frequency));
        }

        return frequency;
    }

    private static void ValidateStartDate(DateOnly startDate)
    {
        if (startDate < new DateOnly(1900, 1, 1))
        {
            throw new ArgumentException(
                $"StartDate must be on or after 1900-01-01. Received: {startDate:o}.",
                nameof(startDate));
        }
    }

    private static void ValidateTotalInstallments(int totalInstallments)
    {
        if (totalInstallments <= 0)
        {
            throw new ArgumentException(
                $"TotalInstallments must be strictly positive. Received: {totalInstallments}.",
                nameof(totalInstallments));
        }
    }

    private static string? ValidateOptionalLength(string? value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException(
                $"{paramName} must be at most {maxLength} characters. Received: {trimmed.Length}.",
                paramName);
        }

        return trimmed;
    }
}
