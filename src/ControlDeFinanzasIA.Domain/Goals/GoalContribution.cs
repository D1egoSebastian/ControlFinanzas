using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Goals;

/// <summary>
/// Aporte a una <see cref="Goal"/>. Cada vez que el usuario transfiere
/// dinero a una meta, se crea un registro de tipo <c>GoalContribution</c>.
/// </summary>
/// <remarks>
/// <para>
/// A diferencia de <c>Domain.Debts.DebtPayment</c> (constructor
/// <c>internal</c>, generado en bloque por
/// <c>DebtScheduleGenerator</c>), <see cref="Create"/> es un factory
/// PUBLICO: los aportes son manuales y ad-hoc, ingresados por el
/// usuario a voluntad a traves de <c>ContributeToGoalUseCase</c> (capa
/// Application), no generados en bloque por un generador de calendario.
/// </para>
/// <para>
/// Setters privados y constructor sin parametros para EF Core, mismo
/// patron que <see cref="Goal"/> y <c>Domain.Debts.Debt</c>.
/// </para>
/// </remarks>
public sealed class GoalContribution
{
    /// <summary>Identificador unico del aporte.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador de la <see cref="Goal"/> due&#241;a de este aporte.</summary>
    public Guid GoalId { get; private set; }

    /// <summary>Fecha en que el dinero fue apartado (dato de negocio real, provisto por el usuario).</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Monto del aporte (positivo).</summary>
    public Money Amount { get; private set; } = null!;

    /// <summary>
    /// Constructor sin parametros reservado para EF Core. No debe
    /// usarse desde codigo de aplicacion.
    /// </summary>
    private GoalContribution()
    {
    }

    /// <summary>
    /// Constructor con parametros para uso interno del factory
    /// <see cref="Create"/>. El caller ya valido las invariantes.
    /// </summary>
    private GoalContribution(Guid id, Guid goalId, DateOnly date, Money amount)
    {
        Id = id;
        GoalId = goalId;
        Date = date;
        Amount = amount;
    }

    /// <summary>
    /// Crea un nuevo aporte validando todas las invariantes (R-3).
    /// PUBLICO a diferencia de <c>DebtPayment</c>: ver comentario de
    /// clase.
    /// </summary>
    /// <param name="goalId">Identificador de la meta a la que pertenece el aporte. No puede ser <see cref="Guid.Empty"/>.</param>
    /// <param name="amount">Monto del aporte (&gt; 0).</param>
    /// <param name="date">Fecha en que el dinero fue apartado.</param>
    /// <returns>Una instancia valida de <see cref="GoalContribution"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="amount"/> es <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Si se viola alguna invariante.</exception>
    public static GoalContribution Create(Guid goalId, Money amount, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(amount);

        ValidateGoalId(goalId);
        ValidateAmount(amount);

        return new GoalContribution(Guid.NewGuid(), goalId, date, amount);
    }

    private static void ValidateGoalId(Guid goalId)
    {
        if (goalId == Guid.Empty)
        {
            throw new ArgumentException("GoalId is required.", nameof(goalId));
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
