using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Goals;

/// <summary>
/// Resultado de <see cref="Goal.CalculateProgress"/>: el total
/// aportado, el remanente y el porcentaje completado.
/// </summary>
/// <param name="Contributed">Suma de todos los aportes validos, en la moneda de la meta.</param>
/// <param name="Remaining">
/// <c>TargetAmount - Contributed</c>, nunca negativo: clamp-eado a
/// <see cref="Money.Zero(Currency)"/> cuando <paramref name="Contributed"/>
/// alcanza o supera el objetivo (el sobre-aporte es normal en una meta
/// de ahorro).
/// </param>
/// <param name="PercentComplete">
/// Porcentaje completado, redondeado. Nunca clamp-eado: una meta
/// sobre-financiada reporta honestamente un valor mayor a 100.
/// </param>
public readonly record struct GoalProgress(Money Contributed, Money Remaining, decimal PercentComplete);

/// <summary>
/// Raiz de agregado que modela una meta de ahorro (F-011 de Fase 11)
/// con monto objetivo, moneda, fecha objetivo y nota opcional.
/// </summary>
/// <remarks>
/// <para>
/// <b>Invariantes (R-2):</b>
/// <list type="bullet">
///   <item>Name no vacio (max <see cref="MaxNameLength"/> chars).</item>
///   <item>TargetAmount positivo (&gt; 0). <see cref="Money.Create"/> solo rechaza
///   montos negativos, no cero, por lo que esta invariante se valida aqui.</item>
///   <item>TargetDate NO tiene restriccion: una fecha objetivo en el
///   pasado es un estado legal y soportado (advertencia en UI/plan, no
///   un rechazo de construccion).</item>
///   <item>Note es opcional (longitud maxima acotada si se provee).</item>
/// </list>
/// </para>
/// <para>
/// <b>Patron de persistencia:</b> setters privados y constructor sin
/// parametros para EF Core, factory estatico <see cref="Create"/> que
/// valida todas las invariantes antes de construir. Mismo patron que
/// <c>Domain.Debts.Debt</c> y <c>Domain.Budgets.Budget</c>.
/// </para>
/// <para>
/// El progreso (R-5) se calcula siempre a partir del ledger de
/// <see cref="GoalContribution"/>, nunca se almacena
/// (<see cref="CalculateProgress"/>). El auto-completado (R-8) vive en
/// el dominio (<see cref="TryAutoComplete"/>) para que sea verificable
/// sin recalcular la logica fuera de esta clase, mismo patron que
/// <c>Debt.CalculatePendingBalance</c> / <c>Debt.TryAutoClose</c>.
/// </para>
/// </remarks>
public sealed class Goal
{
    /// <summary>Longitud maxima del nombre.</summary>
    public const int MaxNameLength = 120;

    /// <summary>Longitud maxima de la nota.</summary>
    public const int MaxNoteLength = 500;

    /// <summary>Decimales del porcentaje completado, igual que <c>BudgetEvaluator</c>.</summary>
    public const int PercentDecimals = 2;

    /// <summary>Identificador unico de la meta.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre de la meta (ej. "Fondo de emergencia").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Monto objetivo de la meta (positivo).</summary>
    public Money TargetAmount { get; private set; } = null!;

    /// <summary>Moneda de la meta (denormalizado, siempre igual a TargetAmount.Currency).</summary>
    public Currency Currency { get; private set; }

    /// <summary>Fecha objetivo. Puede estar en el pasado (dato legal, no restringido).</summary>
    public DateOnly TargetDate { get; private set; }

    /// <summary>Nota libre opcional.</summary>
    public string? Note { get; private set; }

    /// <summary>Estado actual de la meta.</summary>
    public GoalStatus Status { get; private set; } = GoalStatus.Active;

    /// <summary>
    /// Constructor sin parametros reservado para EF Core. No debe
    /// usarse desde codigo de aplicacion.
    /// </summary>
    private Goal()
    {
    }

    /// <summary>
    /// Constructor con parametros para uso interno del factory
    /// <see cref="Create"/>. El caller ya valido las invariantes.
    /// </summary>
    private Goal(
        Guid id,
        string name,
        Money targetAmount,
        Currency currency,
        DateOnly targetDate,
        string? note,
        GoalStatus status)
    {
        Id = id;
        Name = name;
        TargetAmount = targetAmount;
        Currency = currency;
        TargetDate = targetDate;
        Note = note;
        Status = status;
    }

    /// <summary>
    /// Crea una nueva meta validando todas las invariantes (R-2).
    /// </summary>
    /// <param name="name">Nombre de la meta (1..120 chars).</param>
    /// <param name="targetAmount">Monto objetivo (&gt; 0). Su <see cref="Money.Currency"/> se usa como <see cref="Currency"/> de la meta.</param>
    /// <param name="targetDate">Fecha objetivo. NO se valida contra la fecha actual: una fecha pasada es legal.</param>
    /// <param name="note">Nota opcional (max 500 chars).</param>
    /// <returns>Una instancia valida de <see cref="Goal"/> en estado Active.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="targetAmount"/> es <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Si se viola alguna invariante.</exception>
    public static Goal Create(string name, Money targetAmount, DateOnly targetDate, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(targetAmount);

        string normalizedName = ValidateName(name);
        string? normalizedNote = ValidateOptionalLength(note, MaxNoteLength, nameof(note));
        ValidateTargetAmount(targetAmount);
        // NO se valida TargetDate: una fecha objetivo en el pasado es un estado legal (Q2).

        return new Goal(
            Guid.NewGuid(),
            normalizedName,
            targetAmount,
            targetAmount.Currency,
            targetDate,
            normalizedNote,
            GoalStatus.Active);
    }

    /// <summary>
    /// Calcula el progreso de la meta (R-5) sumando
    /// <paramref name="contributions"/>: nunca se re-implementa la
    /// suma fuera de este metodo (ni en queries ni en UI).
    /// </summary>
    /// <param name="contributions">Todos los aportes de esta meta.</param>
    /// <returns>
    /// Un <see cref="GoalProgress"/> con el total aportado, el
    /// remanente (clamp-eado a cero, nunca negativo) y el porcentaje
    /// completado (nunca clamp-eado).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="contributions"/> es <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Se lanza si algun aporte de <paramref name="contributions"/>
    /// esta en una moneda distinta a la de esta meta (defensa en
    /// profundidad; el guard primario vive en
    /// <c>ContributeToGoalUseCase</c>).
    /// </exception>
    public GoalProgress CalculateProgress(IReadOnlyList<GoalContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        foreach (GoalContribution contribution in contributions)
        {
            if (contribution.Amount.Currency != Currency)
            {
                throw new ArgumentException(
                    $"Contribution {contribution.Id} is in {contribution.Amount.Currency} but goal {Id} is in {Currency}.",
                    nameof(contributions));
            }
        }

        Money contributed = contributions.Aggregate(
            Money.Zero(Currency), (total, contribution) => total.Add(contribution.Amount));

        // Money.Subtract throws when the result would be negative. Over-contribution is
        // normal for a goal, so Remaining is computed with an explicit clamp, never an
        // unguarded Subtract.
        Money remaining = contributed.Amount >= TargetAmount.Amount
            ? Money.Zero(Currency)
            : TargetAmount.Subtract(contributed);

        decimal percentComplete = Math.Round(
            contributed.Amount / TargetAmount.Amount * 100m, PercentDecimals, MidpointRounding.AwayFromZero);

        return new GoalProgress(contributed, remaining, percentComplete);
    }

    /// <summary>
    /// Evalua el auto-completado de la meta (R-8): si
    /// <see cref="CalculateProgress"/> alcanza o supera
    /// <see cref="TargetAmount"/>, la meta pasa a
    /// <see cref="GoalStatus.Completed"/>.
    /// </summary>
    /// <param name="contributions">Todos los aportes de esta meta.</param>
    /// <returns>
    /// <see langword="true"/> si la meta se completo como resultado de
    /// esta llamada; <see langword="false"/> si ya estaba completada o
    /// si la condicion de completado no se cumple todavia.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="contributions"/> es <see langword="null"/>.</exception>
    public bool TryAutoComplete(IReadOnlyList<GoalContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        if (Status == GoalStatus.Completed)
        {
            return false;
        }

        GoalProgress progress = CalculateProgress(contributions);
        if (progress.Contributed.Amount < TargetAmount.Amount)
        {
            return false;
        }

        Status = GoalStatus.Completed;
        return true;
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

    private static void ValidateTargetAmount(Money targetAmount)
    {
        if (targetAmount.Amount <= 0)
        {
            throw new ArgumentException(
                $"TargetAmount must be strictly positive. Received: {targetAmount.Amount}.",
                nameof(targetAmount));
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
