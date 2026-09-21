using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Row projection for <see cref="GoalsViewModel.Goals"/>. Progress and
/// plan are precomputed by <c>GetGoalsQuery</c> (F-011, slice 3, D-10);
/// this record never re-implements any arithmetic, it only shapes the
/// query result for binding.
/// </summary>
/// <param name="Id">The goal's identifier.</param>
/// <param name="Name">The goal's display name.</param>
/// <param name="Currency">The goal's currency.</param>
/// <param name="TargetAmount">The goal's target amount.</param>
/// <param name="ContributedAmount">Total contributed so far.</param>
/// <param name="RemainingAmount">Remaining amount, clamped at zero.</param>
/// <param name="PercentComplete">Percent complete, never clamped (can exceed 100).</param>
/// <param name="TargetDate">The goal's target date.</param>
/// <param name="Status">The goal's current status.</param>
/// <param name="MonthsRemaining">Months remaining per the savings plan.</param>
/// <param name="MonthlyContributionRequired">Required monthly contribution per the savings plan.</param>
/// <param name="AlreadyMet">Whether the target has already been reached.</param>
/// <param name="TargetDateInPast">Whether the target date has already elapsed.</param>
public sealed record GoalRow(
    Guid Id,
    string Name,
    Currency Currency,
    decimal TargetAmount,
    decimal ContributedAmount,
    decimal RemainingAmount,
    decimal PercentComplete,
    DateOnly TargetDate,
    GoalStatus Status,
    int MonthsRemaining,
    decimal MonthlyContributionRequired,
    bool AlreadyMet,
    bool TargetDateInPast)
{
    /// <summary>Gets the progress value clamped at 100, for <c>ProgressBar</c> binding only. <see cref="PercentComplete"/> stays truthful.</summary>
    public double ProgressValue => Math.Min((double)PercentComplete, 100d);

    /// <summary>Gets a value indicating whether the goal has been completed.</summary>
    public bool IsCompleted => Status == GoalStatus.Completed;

    /// <summary>
    /// Gets a value indicating whether the "already met" message should be
    /// shown. Presentation precedence for the three mutually-exclusive
    /// messages is resolved here, not in XAML, because
    /// <see cref="AlreadyMet"/> and <see cref="TargetDateInPast"/> can both
    /// be true (design D-4).
    /// </summary>
    public bool ShowAlreadyMetMessage => AlreadyMet;

    /// <summary>Gets a value indicating whether the past-target-date warning should be shown.</summary>
    public bool ShowPastDateWarning => TargetDateInPast && !AlreadyMet;

    /// <summary>Gets a value indicating whether the savings plan should be shown.</summary>
    public bool ShowPlan => !AlreadyMet && !TargetDateInPast;
}
