using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.DTOs.Aggregates;

/// <summary>
/// Proyección plana de un <see cref="Domain.Transactions.Expense"/> dentro
/// de un periodo (F-014, D-7): una fila por gasto, sin agregar. Alimenta
/// <c>DetectAnomaliesUseCase</c>, que necesita el detalle por gasto (no una
/// suma) para construir el historial y los candidatos de
/// <c>AnomalyDetector</c>.
/// </summary>
/// <param name="ExpenseId">Identificador del gasto.</param>
/// <param name="CategoryId">Identificador de la categoría del gasto.</param>
/// <param name="Date">Fecha del gasto.</param>
/// <param name="Amount">Monto sin convertir, en su moneda original.</param>
/// <param name="Currency">Moneda del monto.</param>
/// <param name="Description">Descripción corta del gasto.</param>
public sealed record ExpenseAmountRow(
    Guid ExpenseId, Guid CategoryId, DateOnly Date, decimal Amount, Currency Currency, string Description);
