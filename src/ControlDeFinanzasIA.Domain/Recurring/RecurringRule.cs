using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Recurring;

/// <summary>
/// Base abstracta para reglas recurrentes. Modela suscripciones
/// (un servicio específico) y gastos fijos (alquiler, internet,
/// etc.).
/// </summary>
/// <remarks>
/// <para>
/// Es la raíz de un agregado: encapsula el estado de la regla
/// (Name, Amount, Frequency, StartDate, EndDate, etc.) y garantiza
/// sus invariantes via factory <see cref="Create"/> y método
/// <see cref="Update"/>. Las clases concretas (<see cref="Subscription"/>,
/// <see cref="FixedExpense"/>) son tipos concretos que distinguen
/// el tipo para UI/consultas y para la discriminación TPH de EF Core.
/// </para>
/// <para>
/// <b>Invariantes:</b>
/// <list type="bullet">
///   <item>Name no vacio (max 100 chars).</item>
///   <item>Amount positivo (&gt; 0).</item>
///   <item>StartDate &gt;= 1900-01-01.</item>
///   <item>EndDate es null o &gt;= StartDate.</item>
///   <item>CategoryId no Guid.Empty.</item>
///   <item>PaymentMethodId no Guid.Empty.</item>
///   <item>Frequency es uno de los valores validos de <see cref="RecurringFrequency"/>.</item>
///   <item>NextGenerationDate siempre es &gt;= StartDate.</item>
/// </list>
/// </para>
/// <para>
/// <b>Patrón de persistencia:</b> las propiedades usan
/// setters privados y constructores sin parámetros para que EF Core
/// pueda materializar la entidad sin exponer mutabilidad al exterior.
/// El factory estático <see cref="Create"/> valida las invariantes
/// antes de asignar.
/// </para>
/// </remarks>
public abstract class RecurringRule
{
    /// <summary>Longitud maxima del nombre.</summary>
    public const int MaxNameLength = 100;

    /// <summary>
    /// Identificador unico de la regla. Asignado en
    /// <see cref="Create"/> via <see cref="Guid.NewGuid"/>.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre descriptivo (ej. "Netflix", "Alquiler").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Monto a generar en cada ocurrencia (positivo).</summary>
    public Money Amount { get; private set; } = null!;

    /// <summary>Moneda del monto (denormalizado para queries).</summary>
    public Currency Currency { get; private set; }

    /// <summary>Frecuencia de la regla (Monthly, Bimonthly, Yearly).</summary>
    public string Frequency { get; private set; } = "Monthly";

    /// <summary>Fecha de inicio (y primera ocurrencia).</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>Fecha de fin opcional. Null = indefinida.</summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>Categoria del movimiento a generar.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>Metodo de pago del movimiento a generar.</summary>
    public Guid PaymentMethodId { get; private set; }

    /// <summary>Tags del movimiento a generar.</summary>
    public IReadOnlyList<Guid> TagIds { get; private set; } = Array.Empty<Guid>();

    /// <summary>Si la regla esta activa (false = no genera).</summary>
    public bool Active { get; private set; } = true;

    /// <summary>
    /// Proxima fecha en que la regla debe dispararse. Lo mantiene el
    /// <c>GenerateDueRecurringUseCase</c> despues de generar.
    /// Mutable via <see cref="AdvanceAfterGeneration"/>.
    /// </summary>
    public DateOnly NextGenerationDate { get; private set; }

    /// <summary>
    /// Constructor sin parametros reservado para EF Core. No debe
    /// usarse desde codigo de aplicacion.
    /// </summary>
    protected RecurringRule() { }

    /// <summary>
    /// Constructor con parametros para uso interno (factory
    /// <see cref="Create"/> y los records hijos como
    /// <see cref="Subscription"/>). Valida que el caller ya verifico
    /// las invariantes.
    /// </summary>
    protected RecurringRule(
        Guid id,
        string name,
        Money amount,
        Currency currency,
        string frequency,
        DateOnly startDate,
        DateOnly? endDate,
        Guid categoryId,
        Guid paymentMethodId,
        IReadOnlyList<Guid> tagIds,
        bool active,
        DateOnly nextGenerationDate)
    {
        Id = id;
        Name = name;
        Amount = amount;
        Currency = currency;
        Frequency = frequency;
        StartDate = startDate;
        EndDate = endDate;
        CategoryId = categoryId;
        PaymentMethodId = paymentMethodId;
        TagIds = tagIds;
        Active = active;
        NextGenerationDate = nextGenerationDate;
    }

    /// <summary>
    /// Crea una nueva regla recurrente validando las invariantes.
    /// Devuelve la version <see cref="Subscription"/> por defecto;
    /// las clases hijas especializadas se crean en factories
    /// dedicados (Fase 6 F6-004).
    /// </summary>
    /// <param name="name">Nombre descriptivo (1..100 chars).</param>
    /// <param name="amount">Monto a generar (&gt; 0).</param>
    /// <param name="currency">Moneda del monto.</param>
    /// <param name="frequency">
    /// Frecuencia: <c>"Monthly"</c>, <c>"Bimonthly"</c> o <c>"Yearly"</c>.
    /// </param>
    /// <param name="startDate">Fecha de inicio (y primera ocurrencia).</param>
    /// <param name="endDate">Fecha de fin opcional.</param>
    /// <param name="categoryId">Categoria del movimiento.</param>
    /// <param name="paymentMethodId">Metodo de pago del movimiento.</param>
    /// <param name="tagIds">Tags opcionales.</param>
    /// <param name="firstChargeDate">
    /// Fecha del primer cobro (cursor inicial). Null = <paramref name="startDate"/>. Debe estar dentro de
    /// [<paramref name="startDate"/>, <paramref name="endDate"/>].
    /// </param>
    /// <returns>Una instancia valida de <see cref="Subscription"/>.</returns>
    /// <exception cref="ArgumentException">Si se viola alguna invariante.</exception>
    public static Subscription Create(
        string name,
        Money amount,
        Currency currency,
        string frequency,
        DateOnly startDate,
        DateOnly? endDate,
        Guid categoryId,
        Guid paymentMethodId,
        IReadOnlyList<Guid>? tagIds = null,
        DateOnly? firstChargeDate = null)
    {
        ArgumentNullException.ThrowIfNull(amount);

        string normalizedName = ValidateName(name);
        string normalizedFrequency = ValidateFrequency(frequency);
        ValidateDateRange(startDate, endDate);
        ValidateGuids(categoryId, paymentMethodId);
        if (amount.Amount <= 0)
        {
            throw new ArgumentException(
                $"Amount must be strictly positive. Received: {amount.Amount}.",
                nameof(amount));
        }

        IReadOnlyList<Guid> safeTags = tagIds ?? Array.Empty<Guid>();
        DateOnly firstCharge = ValidateNextCharge(firstChargeDate, startDate, endDate);

        return new Subscription(
            Guid.NewGuid(), normalizedName, amount, currency, normalizedFrequency,
            startDate, endDate, categoryId, paymentMethodId, safeTags, true, firstCharge);
    }

    /// <summary>
    /// Aplica cambios a la regla. Re-valida todas las invariantes.
    /// El Id no se puede cambiar. El cursor <see cref="NextGenerationDate"/> solo se mueve si se indica
    /// <c>nextChargeDate</c> o si la fecha de inicio lo supera; guardar sin cambios nunca mueve el calendario.
    /// </summary>
    public void Update(
        string name,
        Money amount,
        Currency currency,
        string frequency,
        DateOnly startDate,
        DateOnly? endDate,
        Guid categoryId,
        Guid paymentMethodId,
        IReadOnlyList<Guid>? tagIds = null,
        DateOnly? nextChargeDate = null)
    {
        ArgumentNullException.ThrowIfNull(amount);

        string normalizedName = ValidateName(name);
        string normalizedFrequency = ValidateFrequency(frequency);
        ValidateDateRange(startDate, endDate);
        ValidateGuids(categoryId, paymentMethodId);
        if (amount.Amount <= 0)
        {
            throw new ArgumentException(
                $"Amount must be strictly positive. Received: {amount.Amount}.",
                nameof(amount));
        }

        DateOnly? explicitNext = nextChargeDate is null ? null : ValidateNextCharge(nextChargeDate, startDate, endDate);

        Name = normalizedName;
        Amount = amount;
        Currency = currency;
        Frequency = normalizedFrequency;
        StartDate = startDate;
        EndDate = endDate;
        CategoryId = categoryId;
        PaymentMethodId = paymentMethodId;
        TagIds = tagIds ?? TagIds;

        // The schedule cursor only moves when the caller says where the next charge is, or when the start date was
        // pushed past it. Saving an edit that changes neither never moves the schedule.
        if (explicitNext is { } next)
        {
            NextGenerationDate = next;
        }
        else if (NextGenerationDate < startDate)
        {
            NextGenerationDate = startDate;
        }
    }

    /// <summary>
    /// Desactiva la regla. Una regla desactivada no genera movimientos.
    /// </summary>
    public void Deactivate() => Active = false;

    /// <summary>
    /// Activa la regla. Si la fecha actual es posterior a
    /// <see cref="EndDate"/>, la regla queda activa pero sin
    /// generacion efectiva.
    /// </summary>
    public void Activate() => Active = true;

    /// <summary>
    /// Avanza el cursor <see cref="NextGenerationDate"/> despues de
    /// una generacion exitosa. El caller pasa la fecha de la
    /// generacion (no la del cursor anterior) y la frecuencia.
    /// </summary>
    /// <param name="generatedDate">Fecha de la ocurrencia que se acaba de generar.</param>
    /// <param name="monthInterval">Intervalo en meses (1, 2 o 12).</param>
    public void AdvanceAfterGeneration(DateOnly generatedDate, int monthInterval)
    {
        if (generatedDate < NextGenerationDate)
        {
            throw new ArgumentException(
                $"GeneratedDate ({generatedDate:o}) is before NextGenerationDate ({NextGenerationDate:o}).",
                nameof(generatedDate));
        }

        NextGenerationDate = RecurringSchedule.AddMonthsAnchored(StartDate.Day, generatedDate, monthInterval);
    }

    private static DateOnly ValidateNextCharge(DateOnly? nextCharge, DateOnly startDate, DateOnly? endDate)
    {
        if (nextCharge is not { } next)
        {
            return startDate;
        }

        if (next < startDate)
        {
            throw new ArgumentException(
                $"The first charge ({next:o}) must be on or after StartDate ({startDate:o}).",
                nameof(nextCharge));
        }

        if (endDate is { } end && next > end)
        {
            throw new ArgumentException(
                $"The first charge ({next:o}) must be on or before EndDate ({end:o}).",
                nameof(nextCharge));
        }

        return next;
    }

    private static string ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        string trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Name must be at most {MaxNameLength} characters. Received: {trimmed.Length}.",
                nameof(name));
        }

        return trimmed;
    }

    private static string ValidateFrequency(string? frequency)
    {
        if (string.IsNullOrWhiteSpace(frequency))
        {
            throw new ArgumentException("Frequency is required.", nameof(frequency));
        }

        bool valid = frequency is nameof(RecurringFrequency.Monthly)
            or nameof(RecurringFrequency.Bimonthly)
            or nameof(RecurringFrequency.Yearly);
        if (!valid)
        {
            throw new ArgumentException(
                $"Frequency must be one of: Monthly, Bimonthly, Yearly. Received: {frequency}.",
                nameof(frequency));
        }

        return frequency;
    }

    private static void ValidateDateRange(DateOnly startDate, DateOnly? endDate)
    {
        if (startDate < new DateOnly(1900, 1, 1))
        {
            throw new ArgumentException(
                $"StartDate must be on or after 1900-01-01. Received: {startDate:o}.",
                nameof(startDate));
        }

        if (endDate is { } end && end < startDate)
        {
            throw new ArgumentException(
                $"EndDate ({end:o}) must be on or after StartDate ({startDate:o}).",
                nameof(endDate));
        }
    }

    private static void ValidateGuids(Guid categoryId, Guid paymentMethodId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("CategoryId is required.", nameof(categoryId));
        }

        if (paymentMethodId == Guid.Empty)
        {
            throw new ArgumentException("PaymentMethodId is required.", nameof(paymentMethodId));
        }
    }
}
