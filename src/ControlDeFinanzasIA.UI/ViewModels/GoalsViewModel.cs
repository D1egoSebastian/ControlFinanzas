using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Goals.ContributeToGoal;
using ControlDeFinanzasIA.Application.UseCases.Goals.CreateGoal;
using ControlDeFinanzasIA.Application.UseCases.Goals.DeleteGoal;
using ControlDeFinanzasIA.Application.UseCases.Goals.GetGoals;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.UI.Resources;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Management screen for savings goals (F-011, slice 3, R-1): create a
/// goal, register a manual contribution against a selected goal, and
/// delete a goal. Follows the <see cref="BudgetsViewModel"/> template —
/// fire-and-forget constructor load, flat observable form fields, an
/// observable row list, and <c>Result</c>-driven error surfacing that
/// never throws into the UI (D-10).
/// </summary>
public sealed partial class GoalsViewModel : ViewModelBase
{
    private readonly GetGoalsQuery _getGoalsQuery;
    private readonly CreateGoalUseCase _createUseCase;
    private readonly ContributeToGoalUseCase _contributeUseCase;
    private readonly DeleteGoalUseCase _deleteUseCase;
    private readonly IClock _clock;
    private readonly ILogger<GoalsViewModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoalsViewModel"/>
    /// class. Defaults the create-form target date six months out and the
    /// contribution date to today, then kicks off the initial load,
    /// following the same constructor-load pattern as
    /// <see cref="BudgetsViewModel"/>.
    /// </summary>
    public GoalsViewModel(
        GetGoalsQuery getGoalsQuery,
        CreateGoalUseCase createUseCase,
        ContributeToGoalUseCase contributeUseCase,
        DeleteGoalUseCase deleteUseCase,
        IClock clock,
        ILogger<GoalsViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(getGoalsQuery);
        ArgumentNullException.ThrowIfNull(createUseCase);
        ArgumentNullException.ThrowIfNull(contributeUseCase);
        ArgumentNullException.ThrowIfNull(deleteUseCase);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _getGoalsQuery = getGoalsQuery;
        _createUseCase = createUseCase;
        _contributeUseCase = contributeUseCase;
        _deleteUseCase = deleteUseCase;
        _clock = clock;
        _logger = logger;

        Currencies = Enum.GetValues<Currency>();
        _targetDate = _clock.Today.AddMonths(6);
        _contributionDate = _clock.Today;

        _ = LoadAsync();
    }

    // --- create form ---

    /// <summary>Gets or sets the name entered in the create-goal form.</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>Gets or sets the target amount entered in the create-goal form.</summary>
    [ObservableProperty]
    private decimal _targetAmount;

    /// <summary>Gets or sets the currency selected in the create-goal form.</summary>
    [ObservableProperty]
    private Currency _currency = Currency.DOP;

    /// <summary>Gets or sets the target date entered in the create-goal form.</summary>
    [ObservableProperty]
    private DateOnly _targetDate;

    /// <summary>Gets or sets the optional note entered in the create-goal form.</summary>
    [ObservableProperty]
    private string? _note;

    // --- contribution sub-flow (visible only when SelectedGoal is not null) ---

    /// <summary>
    /// Gets or sets the goal row currently selected for contribution.
    /// Setting this clears <see cref="FormError"/> and resets
    /// <see cref="ContributionAmount"/> via
    /// <see cref="OnSelectedGoalChanged"/>.
    /// </summary>
    [ObservableProperty]
    private GoalRow? _selectedGoal;

    /// <summary>Gets or sets the amount entered in the contribution form.</summary>
    [ObservableProperty]
    private decimal _contributionAmount;

    /// <summary>Gets or sets the date entered in the contribution form.</summary>
    [ObservableProperty]
    private DateOnly _contributionDate;

    // --- shared chrome, BudgetsViewModel shape ---

    /// <summary>Gets or sets the error message shown when an operation fails.</summary>
    [ObservableProperty]
    private string _formError = string.Empty;

    /// <summary>Gets or sets the status message shown after a successful operation.</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Gets or sets a value indicating whether a load is in progress.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Gets the goal rows, with progress and plan precomputed by <see cref="GetGoalsQuery"/>.</summary>
    public ObservableCollection<GoalRow> Goals { get; } = [];

    /// <summary>Gets the currencies available in the create-goal currency picker.</summary>
    public IReadOnlyList<Currency> Currencies { get; }

    /// <summary>
    /// Gets the currency of the pending contribution, pinned to
    /// <see cref="SelectedGoal"/>'s currency. Read-only in the UI: the
    /// contribution form has no currency picker, so a currency mismatch
    /// (R-4) is unreachable from this screen (D-10) and is enforced, and
    /// tested, at the use-case layer.
    /// </summary>
    public Currency? ContributionCurrency => SelectedGoal?.Currency;

    partial void OnSelectedGoalChanged(GoalRow? value)
    {
        FormError = string.Empty;
        ContributionAmount = 0;
        OnPropertyChanged(nameof(ContributionCurrency));
    }

    /// <summary>Selects a goal row, revealing the contribution panel for it.</summary>
    /// <param name="row">The row to select. Null is treated as no-op.</param>
    [RelayCommand]
    private void Select(GoalRow? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedGoal = row;
    }

    /// <summary>Creates a new savings goal from the current create-form values (R-1).</summary>
    [RelayCommand]
    private async Task CreateAsync()
    {
        FormError = string.Empty;

        var command = new CreateGoalCommand(Name, TargetAmount, Currency, TargetDate, Note);

        Result result = await _createUseCase.ExecuteAsync(command).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            FormError = result.Error?.Message ?? Strings.GoalsErrorMessage;
            return;
        }

        StatusMessage = Strings.GoalsCreatedMessage;
        ResetCreateForm();
        await LoadGoalsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Registers a contribution against <see cref="SelectedGoal"/> (R-3).
    /// The command is always built with the selected goal's currency,
    /// never a user-picked one (Q1/D-10). When the use case reports a
    /// <c>CurrencyMismatch</c> failure, the localized
    /// <see cref="Strings.GoalsCurrencyMismatchMessage"/> is substituted
    /// for <see cref="FormError"/> instead of the use case's internal
    /// message (spec R-4 reconciliation note).
    /// </summary>
    [RelayCommand]
    private async Task ContributeAsync()
    {
        FormError = string.Empty;

        if (SelectedGoal is null)
        {
            return;
        }

        var command = new ContributeToGoalCommand(
            SelectedGoal.Id, ContributionAmount, SelectedGoal.Currency, ContributionDate);

        Result result = await _contributeUseCase.ExecuteAsync(command).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            FormError = result.Error?.Code == "CurrencyMismatch"
                ? Strings.GoalsCurrencyMismatchMessage
                : result.Error?.Message ?? Strings.GoalsErrorMessage;
            return;
        }

        StatusMessage = Strings.GoalsContributedMessage;
        ContributionAmount = 0;
        await LoadGoalsAsync().ConfigureAwait(false);
    }

    /// <summary>Deletes the given goal row (hard delete, R-1).</summary>
    /// <param name="row">The row to delete. Null is treated as no-op.</param>
    [RelayCommand]
    private async Task DeleteAsync(GoalRow? row)
    {
        if (row is null)
        {
            return;
        }

        FormError = string.Empty;
        Result result = await _deleteUseCase.ExecuteAsync(row.Id).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            FormError = result.Error?.Message ?? Strings.GoalsErrorMessage;
            return;
        }

        StatusMessage = Strings.GoalsDeletedMessage;
        if (SelectedGoal?.Id == row.Id)
        {
            SelectedGoal = null;
        }

        await LoadGoalsAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        FormError = string.Empty;

        try
        {
            await LoadGoalsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load goals.");
            FormError = Strings.GoalsLoadErrorMessage;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadGoalsAsync()
    {
        Result result = await _getGoalsQuery.ExecuteAsync().ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            FormError = result.Error?.Message ?? Strings.GoalsLoadErrorMessage;
            return;
        }

        Goals.Clear();
        foreach (GoalPlanItem item in (IReadOnlyList<GoalPlanItem>)result.Value!)
        {
            Goals.Add(new GoalRow(
                item.GoalId,
                item.Name,
                item.Currency,
                item.TargetAmount,
                item.ContributedAmount,
                item.RemainingAmount,
                item.PercentComplete,
                item.TargetDate,
                item.Status,
                item.MonthsRemaining,
                item.MonthlyContributionRequired,
                item.AlreadyMet,
                item.TargetDateInPast));
        }
    }

    private void ResetCreateForm()
    {
        Name = string.Empty;
        TargetAmount = 0;
        Currency = Currency.DOP;
        TargetDate = _clock.Today.AddMonths(6);
        Note = null;
        FormError = string.Empty;
    }
}
