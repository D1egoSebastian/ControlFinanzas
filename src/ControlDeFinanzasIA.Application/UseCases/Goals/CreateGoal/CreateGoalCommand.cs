using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Goals.CreateGoal;

/// <summary>
/// Command para crear una meta de ahorro (R-1, R-2). <see cref="Currency"/>
/// se usa junto con <see cref="TargetAmount"/> para construir el
/// <c>Money</c> objetivo; la moneda de la meta queda derivada de ese
/// <c>Money</c> (D-1), nunca pasada por separado a <c>Goal.Create</c>.
/// </summary>
public sealed record CreateGoalCommand(
    string Name,
    decimal TargetAmount,
    Currency Currency,
    DateOnly TargetDate,
    string? Note);
