using ControlDeFinanzasIA.Domain.Budgets;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// A single row of the Dashboard's budget-progress card (F-010, R-7,
/// D-11): one bar per budgeted category for the currently navigated
/// month, projected from a <c>BudgetVsActualLine</c>.
/// </summary>
/// <param name="CategoryId">Identifier of the budgeted category.</param>
/// <param name="CategoryName">Display name of the budgeted category.</param>
/// <param name="AmountsDisplay">
/// Pre-formatted "≈ {actual} / ≈ {budget}" pair, both already normalized
/// to DOP via <see cref="Formatting.MoneyFormat.ApproximateDop"/>.
/// </param>
/// <param name="PercentDisplay">
/// The TRUE, unclamped percent consumed as display text (e.g. "133.3%"),
/// or the indeterminate placeholder when <see cref="Status"/> is
/// <see langword="null"/> (R-5, S-6). Never a silently wrong 0%/100%.
/// </param>
/// <param name="ProgressValue">
/// The percent consumed clamped to [0, 100] for the progress bar itself
/// — clamping is a bar-rendering concern only, so <see cref="PercentDisplay"/>
/// stays truthful for an over-budget category.
/// </param>
/// <param name="Status">
/// Budget status, or <see langword="null"/> when the line is
/// indeterminate (a missing conversion rate on either side, R-5, S-6).
/// </param>
public sealed record BudgetProgressItem(
    Guid CategoryId,
    string CategoryName,
    string AmountsDisplay,
    string PercentDisplay,
    double ProgressValue,
    BudgetStatus? Status)
{
    /// <summary>
    /// <see langword="true"/> when this line could not be evaluated
    /// because a DOP conversion failed on either side (R-5, S-6). The UI
    /// MUST NOT render this as <see cref="BudgetStatus.Ok"/>, <see cref="BudgetStatus.Near"/>,
    /// or <see cref="BudgetStatus.Exceeded"/>.
    /// </summary>
    public bool IsIndeterminate => Status is null;

    /// <summary>Gets a value indicating whether this line is at or above the near-limit threshold.</summary>
    public bool IsNear => Status == BudgetStatus.Near;

    /// <summary>Gets a value indicating whether this line is at or above budget.</summary>
    public bool IsExceeded => Status == BudgetStatus.Exceeded;
}
