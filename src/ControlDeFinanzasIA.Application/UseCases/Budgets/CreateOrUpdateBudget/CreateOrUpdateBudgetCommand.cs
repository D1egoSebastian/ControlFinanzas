using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Budgets.CreateOrUpdateBudget;

/// <summary>
/// Command para crear o actualizar (upsert) el presupuesto de una
/// categoría en un mes calendario (R-1, R-2). <see cref="CategoryId"/>
/// es nullable en el command para poder rechazar explícitamente los
/// presupuestos globales (R-8, S-9) en el validador en lugar de que
/// el binding falle antes de producir un mensaje útil.
/// </summary>
public sealed record CreateOrUpdateBudgetCommand(
    int Year,
    int Month,
    Guid? CategoryId,
    decimal Amount,
    Currency Currency);
