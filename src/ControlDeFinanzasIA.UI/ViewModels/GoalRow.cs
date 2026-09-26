using CommunityToolkit.Mvvm.ComponentModel;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Row projection for <see cref="GoalsViewModel.Goals"/>. Progress and
/// plan are precomputed by <c>GetGoalsQuery</c> (F-011, slice 3, D-10);
/// this class never re-implements any arithmetic, it only shapes the
/// query result for binding. Converted from an immutable record to an
/// observable class (F-016, D-6) so the per-row AI narration
/// (<see cref="PlanNarration"/>, <see cref="PlanNarrationStatusText"/>,
/// <see cref="IsGeneratingPlan"/>) can be updated in place without
/// rebuilding the row on every keystroke of an in-flight request.
/// </summary>
public sealed partial class GoalRow : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GoalRow"/> class.
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
    /// <remarks>
    /// The parameter names are kept PascalCase, identical to the original record's positional
    /// parameters, so that <c>GoalsViewModelTests</c>'s named-argument call sites keep compiling
    /// unchanged (F-016 proposal binding decision, spec Reconciliation #7). Record positional
    /// parameters are exempt from the project's parameter-naming analyzer rule; a plain class's
    /// constructor parameters are not, hence this scoped suppression.
    /// </remarks>
#pragma warning disable IDE1006 // Naming rule violation: PascalCase constructor parameters, kept to match the former record's positional parameter names (see remarks above).
    public GoalRow(
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
#pragma warning restore IDE1006
    {
        this.Id = Id;
        this.Name = Name;
        this.Currency = Currency;
        this.TargetAmount = TargetAmount;
        this.ContributedAmount = ContributedAmount;
        this.RemainingAmount = RemainingAmount;
        this.PercentComplete = PercentComplete;
        this.TargetDate = TargetDate;
        this.Status = Status;
        this.MonthsRemaining = MonthsRemaining;
        this.MonthlyContributionRequired = MonthlyContributionRequired;
        this.AlreadyMet = AlreadyMet;
        this.TargetDateInPast = TargetDateInPast;
    }

    /// <summary>Gets the goal's identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the goal's display name.</summary>
    public string Name { get; }

    /// <summary>Gets the goal's currency.</summary>
    public Currency Currency { get; }

    /// <summary>Gets the goal's target amount.</summary>
    public decimal TargetAmount { get; }

    /// <summary>Gets the total contributed so far.</summary>
    public decimal ContributedAmount { get; }

    /// <summary>Gets the remaining amount, clamped at zero.</summary>
    public decimal RemainingAmount { get; }

    /// <summary>Gets the percent complete, never clamped (can exceed 100).</summary>
    public decimal PercentComplete { get; }

    /// <summary>Gets the goal's target date.</summary>
    public DateOnly TargetDate { get; }

    /// <summary>Gets the goal's current status.</summary>
    public GoalStatus Status { get; }

    /// <summary>Gets the months remaining per the savings plan.</summary>
    public int MonthsRemaining { get; }

    /// <summary>Gets the required monthly contribution per the savings plan.</summary>
    public decimal MonthlyContributionRequired { get; }

    /// <summary>Gets a value indicating whether the target has already been reached.</summary>
    public bool AlreadyMet { get; }

    /// <summary>Gets a value indicating whether the target date has already elapsed.</summary>
    public bool TargetDateInPast { get; }

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

    /// <summary>
    /// Gets or sets the AI-generated narration text for this row's savings
    /// plan (F-016, R-1, R-6). Empty until <see cref="GoalsViewModel"/>'s
    /// request-plan command sets it, and never carried over across a
    /// reload — <c>LoadGoalsAsync</c> always constructs a fresh
    /// <see cref="GoalRow"/> instance.
    /// </summary>
    [ObservableProperty]
    private string _planNarration = string.Empty;

    /// <summary>
    /// Gets or sets the localized fallback caption shown in place of
    /// <see cref="PlanNarration"/> while a request is in flight or when
    /// the AI is not configured/unavailable (F-016, R-4, R-6). Empty when
    /// <see cref="PlanNarration"/> holds a real narration.
    /// </summary>
    [ObservableProperty]
    private string _planNarrationStatusText = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether a plan-narration request is
    /// currently in flight for this row (F-016, R-7, D-8). Scoped to this
    /// row only, so a request on one row never disables another row's
    /// button.
    /// </summary>
    [ObservableProperty]
    private bool _isGeneratingPlan;
}
