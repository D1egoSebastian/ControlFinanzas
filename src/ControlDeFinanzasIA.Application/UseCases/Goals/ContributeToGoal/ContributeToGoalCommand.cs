using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Goals.ContributeToGoal;

/// <summary>
/// Command para registrar un aporte manual a una meta de ahorro
/// existente (R-3, D-6). <see cref="Currency"/> es la moneda ingresada
/// por el usuario para este aporte — comparada contra la moneda de la
/// meta por el use case (R-4), nunca convertida.
/// </summary>
public sealed record ContributeToGoalCommand(
    Guid GoalId,
    decimal Amount,
    Currency Currency,
    DateOnly Date);
