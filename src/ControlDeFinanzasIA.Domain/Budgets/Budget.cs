using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Budgets;

/// <summary>
/// Raiz de agregado que modela el presupuesto mensual de una categoria
/// (F-010 de Fase 10). Un <see cref="Budget"/> asocia un monto a un
/// periodo (<see cref="Year"/>/<see cref="Month"/>) y una categoria.
/// </summary>
/// <remarks>
/// <para>
/// <b>Invariantes (R-3):</b>
/// <list type="bullet">
///   <item><see cref="Year"/> entre <see cref="MinYear"/> y <see cref="MaxYear"/>.</item>
///   <item><see cref="Month"/> entre 1 y 12.</item>
///   <item><see cref="CategoryId"/> distinto de <see cref="Guid.Empty"/>.</item>
///   <item><see cref="Amount"/> estrictamente positivo (&gt; 0).</item>
/// </list>
/// </para>
/// <para>
/// <b>Patron de persistencia:</b> setters privados y constructor sin
/// parametros para EF Core, factory estatico <see cref="Create"/> que
/// valida todas las invariantes antes de construir. Mismo patron que
/// <c>Debt</c> (Fase 7). Reemplaza el stub anterior (<c>sealed record</c>
/// con setters <c>init</c> publicos) que permitia construir presupuestos
/// invalidos (incluyendo monto cero) desde cualquier capa.
/// </para>
/// <para>
/// <see cref="CategoryId"/> se mantiene nullable en el esquema para
/// futuro soporte de presupuestos globales, pero <see cref="Create"/>
/// exige un <see cref="Guid"/> no nulo: en v1 un <see cref="Budget"/>
/// sin categoria es inalcanzable desde el dominio.
/// </para>
/// </remarks>
public sealed class Budget
{
    /// <summary>Año minimo aceptado.</summary>
    public const int MinYear = 1900;

    /// <summary>Año maximo aceptado.</summary>
    public const int MaxYear = 2999;

    /// <summary>Identificador unico del presupuesto.</summary>
    public Guid Id { get; private set; }

    /// <summary>Año del periodo presupuestado.</summary>
    public int Year { get; private set; }

    /// <summary>Mes del periodo presupuestado (1-12).</summary>
    public int Month { get; private set; }

    /// <summary>
    /// Categoria presupuestada. Nullable en el esquema para
    /// futuro-proofing de presupuestos globales; nunca null en v1
    /// (<see cref="Create"/> exige un <see cref="Guid"/> no nulo).
    /// </summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>Monto presupuestado (positivo).</summary>
    public Money Amount { get; private set; } = null!;

    /// <summary>Moneda del presupuesto (denormalizado, igual que Amount.Currency).</summary>
    public Currency Currency { get; private set; }

    /// <summary>
    /// Constructor sin parametros reservado para EF Core. No debe
    /// usarse desde codigo de aplicacion.
    /// </summary>
    private Budget()
    {
    }

    /// <summary>
    /// Constructor con parametros para uso interno del factory
    /// <see cref="Create"/>. El caller ya valido las invariantes.
    /// </summary>
    private Budget(Guid id, int year, int month, Guid? categoryId, Money amount, Currency currency)
    {
        Id = id;
        Year = year;
        Month = month;
        CategoryId = categoryId;
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Crea un nuevo presupuesto validando todas las invariantes (R-3).
    /// </summary>
    /// <param name="year">Año del periodo (<see cref="MinYear"/>..<see cref="MaxYear"/>).</param>
    /// <param name="month">Mes del periodo (1..12).</param>
    /// <param name="categoryId">Categoria presupuestada (distinta de <see cref="Guid.Empty"/>).</param>
    /// <param name="amount">Monto presupuestado (&gt; 0).</param>
    /// <returns>Una instancia valida de <see cref="Budget"/>.</returns>
    /// <exception cref="ArgumentException">Si se viola alguna invariante.</exception>
    public static Budget Create(int year, int month, Guid categoryId, Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        ValidateYear(year);
        ValidateMonth(month);
        ValidateCategoryId(categoryId);
        ValidateAmount(amount);

        return new Budget(Guid.NewGuid(), year, month, categoryId, amount, amount.Currency);
    }

    /// <summary>
    /// Reemplaza el monto presupuestado (y su moneda) en el lugar. Es
    /// el seam de mutacion que permite el upsert (R-2): los setters
    /// privados hacen a <see cref="Budget"/> inmutable desde
    /// Application salvo por este metodo.
    /// </summary>
    /// <param name="amount">Nuevo monto (&gt; 0).</param>
    /// <exception cref="ArgumentException">Si <paramref name="amount"/> no es estrictamente positivo.</exception>
    public void ChangeAmount(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ValidateAmount(amount);
        Amount = amount;
        Currency = amount.Currency;
    }

    private static void ValidateYear(int year)
    {
        if (year < MinYear || year > MaxYear)
        {
            throw new ArgumentException(
                $"Year must be between {MinYear} and {MaxYear}. Received: {year}.",
                nameof(year));
        }
    }

    private static void ValidateMonth(int month)
    {
        if (month < 1 || month > 12)
        {
            throw new ArgumentException(
                $"Month must be between 1 and 12. Received: {month}.",
                nameof(month));
        }
    }

    private static void ValidateCategoryId(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "CategoryId must not be Guid.Empty.",
                nameof(categoryId));
        }
    }

    private static void ValidateAmount(Money amount)
    {
        if (amount.Amount <= 0)
        {
            throw new ArgumentException(
                $"Amount must be strictly positive. Received: {amount.Amount}.",
                nameof(amount));
        }
    }
}
