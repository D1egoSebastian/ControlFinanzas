using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Row projection for <see cref="BudgetsViewModel.Budgets"/>: joins a
/// <see cref="Domain.Budgets.Budget"/> with its category display name,
/// since <c>Budget</c> itself carries only a <c>CategoryId</c> (F-010,
/// slice 3, D-10).
/// </summary>
/// <param name="Id">The budget's identifier.</param>
/// <param name="CategoryId">The budgeted category's identifier.</param>
/// <param name="CategoryName">The budgeted category's display name.</param>
/// <param name="Amount">The budgeted amount.</param>
/// <param name="Currency">The budgeted amount's currency.</param>
public sealed record BudgetRow(Guid Id, Guid CategoryId, string CategoryName, decimal Amount, Currency Currency);
