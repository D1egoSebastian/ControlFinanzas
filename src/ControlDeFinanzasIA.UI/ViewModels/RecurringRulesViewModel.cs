using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateSubscription;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeactivateRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeleteRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.GenerateDueRecurring;
using ControlDeFinanzasIA.Application.UseCases.Recurring.SkipNextOccurrence;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateSubscription;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.UI.Converters;
using ControlDeFinanzasIA.UI.Resources;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>A month of the year as offered by the yearly-charge month selector.</summary>
/// <param name="Number">The month number (1-12).</param>
/// <param name="Name">The localized month name.</param>
public sealed record MonthOption(int Number, string Name);

/// <summary>
/// Screen for creating and editing recurring rules (subscriptions and fixed monthly expenses). A charge is defined by
/// the day of the month it always happens on (plus the month, for a yearly charge); the first charge is derived from
/// it and shown read-only, and saving an edit never moves the schedule unless the user changes it.
/// </summary>
public sealed partial class RecurringRulesViewModel : ViewModelBase
{
    private readonly IRecurringRuleRepository _repository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPaymentMethodRepository _paymentMethodRepository;
    private readonly CreateSubscriptionUseCase _createUseCase;
    private readonly UpdateSubscriptionUseCase _updateUseCase;
    private readonly CreateFixedExpenseUseCase _createFixedExpenseUseCase;
    private readonly UpdateFixedExpenseUseCase _updateFixedExpenseUseCase;
    private readonly GenerateDueRecurringUseCase _generateUseCase;
    private readonly DeactivateRecurringRuleUseCase _deactivateUseCase;
    private readonly DeleteRecurringRuleUseCase _deleteUseCase;
    private readonly SkipNextOccurrenceUseCase _skipUseCase;
    private readonly IClock _clock;
    private readonly ILogger<RecurringRulesViewModel> _logger;

    public RecurringRulesViewModel(
        IRecurringRuleRepository repository,
        ICategoryRepository categoryRepository,
        IPaymentMethodRepository paymentMethodRepository,
        CreateSubscriptionUseCase createUseCase,
        UpdateSubscriptionUseCase updateUseCase,
        CreateFixedExpenseUseCase createFixedExpenseUseCase,
        UpdateFixedExpenseUseCase updateFixedExpenseUseCase,
        GenerateDueRecurringUseCase generateUseCase,
        DeactivateRecurringRuleUseCase deactivateUseCase,
        DeleteRecurringRuleUseCase deleteUseCase,
        SkipNextOccurrenceUseCase skipUseCase,
        IClock clock,
        ILogger<RecurringRulesViewModel> logger)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _paymentMethodRepository = paymentMethodRepository;
        _createUseCase = createUseCase;
        _updateUseCase = updateUseCase;
        _createFixedExpenseUseCase = createFixedExpenseUseCase;
        _updateFixedExpenseUseCase = updateFixedExpenseUseCase;
        _generateUseCase = generateUseCase;
        _deactivateUseCase = deactivateUseCase;
        _deleteUseCase = deleteUseCase;
        _skipUseCase = skipUseCase;
        _clock = clock;
        _logger = logger;
        _dayOfMonth = clock.Today.Day;
        _month = clock.Today.Month;
        Frequencies = ["Monthly", "Bimonthly", "Yearly"];
        Currencies = Enum.GetValues<Currency>();
        DaysOfMonth = [.. Enumerable.Range(1, 31)];
        Months = [.. Enumerable.Range(1, 12).Select(n => new MonthOption(n, MonthName(n)))];
        Rules.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyNoData));
        _ = LoadAsync();
    }

    public ObservableCollection<RecurringRule> Rules { get; } = [];
    public IReadOnlyList<Category> Categories { get; private set; } = [];
    public IReadOnlyList<PaymentMethod> PaymentMethods { get; private set; } = [];
    public IReadOnlyList<string> Frequencies { get; }
    public IReadOnlyList<string> RuleTypes { get; } = ["Subscription", "FixedExpense"];
    public IReadOnlyList<Currency> Currencies { get; }

    /// <summary>Gets the days of the month a charge can happen on (1-31).</summary>
    public IReadOnlyList<int> DaysOfMonth { get; }

    /// <summary>Gets the months a yearly charge can happen in.</summary>
    public IReadOnlyList<MonthOption> Months { get; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private Currency _currency = Currency.DOP;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMonthVisible))]
    [NotifyPropertyChangedFor(nameof(NextChargeText))]
    private string _frequency = "Monthly";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuleTypeHelp))]
    private string _ruleType = "Subscription";

    /// <summary>Gets or sets the day of the month the charge always happens on (1-31).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NextChargeText))]
    private int _dayOfMonth;

    /// <summary>Gets or sets the month (1-12) of a yearly charge.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NextChargeText))]
    private int _month;

    [ObservableProperty] private DateOnly? _endDate;
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private PaymentMethod? _selectedPaymentMethod;
    [ObservableProperty] private RecurringRule? _selectedRule;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _formError = string.Empty;
    [ObservableProperty] private string _generationError = string.Empty;
    [ObservableProperty] private bool _isGenerating;
    /// <summary>
    /// Gets a value indicating whether the list has loaded successfully at least once. The empty state is never
    /// shown before that, so a slow or failed first load never flashes "no data yet" (F-021, DD-9).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyNoData))]
    private bool _hasLoaded;

    /// <summary>
    /// Gets a value indicating whether the list is loaded and empty: there is no data yet (no filter exists here).
    /// </summary>
    public bool IsEmptyNoData => HasLoaded && Rules.Count == 0;

    /// <summary>Gets a value indicating whether the month selector is shown (yearly charges only).</summary>
    public bool IsMonthVisible => Frequency == nameof(RecurringFrequency.Yearly);

    /// <summary>Gets the plain-language explanation of the selected rule type.</summary>
    public string RuleTypeHelp => RuleType == "FixedExpense"
        ? Strings.RecurringTypeHelpFixedExpense
        : Strings.RecurringTypeHelpSubscription;

    /// <summary>
    /// Gets the read-only "Next charge" line: the stored next charge of the rule being edited while its schedule is
    /// untouched, otherwise the first charge that the chosen day, month and frequency would give.
    /// </summary>
    public string NextChargeText
    {
        get
        {
            try
            {
                DateOnly next = SelectedRule is { } rule && !ScheduleChanged
                    ? rule.NextGenerationDate
                    : DerivedFirstCharge();
                return NextChargeTextConverter.Format(next);
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }
    }

    public ObservableCollection<RecurringGenerationPreview> PreviewItems { get; } = [];

    public bool IsEditing => SelectedRule is not null;

    public static bool IsEditableRule(RecurringRule rule) => rule is Subscription or FixedExpense;

    private bool ScheduleChanged => SelectedRule is { } rule
        && (Frequency != rule.Frequency
            || DayOfMonth != rule.StartDate.Day
            || (IsMonthVisible && Month != rule.StartDate.Month));

    private static string MonthName(int month)
    {
        string name = CultureInfo.CurrentUICulture.DateTimeFormat.GetMonthName(month);
        return name.Length == 0 ? name : char.ToUpper(name[0], CultureInfo.CurrentUICulture) + name[1..];
    }

    private DateOnly DerivedFirstCharge() =>
        RecurringSchedule.FirstCharge(Frequency, DayOfMonth, IsMonthVisible ? Month : null, _clock.Today);

    partial void OnSelectedRuleChanged(RecurringRule? value)
    {
        if (value is null)
        {
            ResetForm();
        }
        else
        {
            RuleType = value is FixedExpense ? "FixedExpense" : "Subscription";
            Name = value.Name;
            Amount = value.Amount.Amount;
            Currency = value.Currency;
            Frequency = value.Frequency;
            DayOfMonth = value.StartDate.Day;
            Month = value.StartDate.Month;
            EndDate = value.EndDate;
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == value.CategoryId);
            SelectedPaymentMethod = PaymentMethods.FirstOrDefault(p => p.Id == value.PaymentMethodId);
        }

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(NextChargeText));
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        FormError = string.Empty;
        if (SelectedCategory is null || SelectedPaymentMethod is null)
        {
            FormError = Strings.RecurringSelectionRequired;
            return;
        }

        if (DayOfMonth is < 1 or > 31 || (IsMonthVisible && Month is < 1 or > 12))
        {
            FormError = Strings.RecurringScheduleRequired;
            return;
        }

        // An untouched schedule is never recomputed: the stored start date and next charge go back as they are.
        DateOnly start;
        DateOnly? next;
        if (SelectedRule is { } edited && !ScheduleChanged)
        {
            start = edited.StartDate;
            next = null;
        }
        else
        {
            DateOnly first = DerivedFirstCharge();
            start = RecurringSchedule.AnchorStartDate(Frequency, DayOfMonth, IsMonthVisible ? Month : null, first);
            next = first;
        }

        DateOnly? end = EndDate;
        Result result = SelectedRule is null && RuleType == "Subscription"
            ? await _createUseCase.ExecuteAsync(new CreateSubscriptionCommand(
                Name, Amount, Currency, Frequency, start, end,
                SelectedCategory.Id, SelectedPaymentMethod.Id, null, next))
            : SelectedRule is null
            ? await _createFixedExpenseUseCase.ExecuteAsync(new CreateFixedExpenseCommand(
                Name, Amount, Currency, Frequency, start, end,
                SelectedCategory.Id, SelectedPaymentMethod.Id, null, next))
            : RuleType == "Subscription"
            ? await _updateUseCase.ExecuteAsync(new UpdateSubscriptionCommand(
                SelectedRule.Id, Name, Amount, Currency, Frequency, start, end,
                SelectedCategory.Id, SelectedPaymentMethod.Id, null, next))
            : await _updateFixedExpenseUseCase.ExecuteAsync(new UpdateFixedExpenseCommand(
                SelectedRule.Id, Name, Amount, Currency, Frequency, start, end,
                SelectedCategory.Id, SelectedPaymentMethod.Id, null, next));

        if (!result.IsSuccess)
        {
            FormError = result.Error?.Message ?? Strings.RecurringSaveFailed;
            return;
        }

        StatusMessage = SelectedRule is null
            ? Strings.RecurringCreated
            : Strings.RecurringUpdated;
        await LoadRulesAsync();
        EndEntry();
    }

    [RelayCommand]
    private void CancelEdit()
    {
        EndEntry();
        FormError = string.Empty;
    }

    [RelayCommand]
    private void Edit(RecurringRule? rule)
    {
        if (rule is not null && IsEditableRule(rule))
        {
            SelectedRule = rule;
        }
    }

    // Leaves the form ready for a new rule: clearing the selection resets it, and when nothing was selected (a
    // creation) it is reset directly, so pressing Save twice can never register the same rule twice.
    private void EndEntry()
    {
        if (SelectedRule is null)
        {
            ResetForm();
        }
        else
        {
            SelectedRule = null;
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            Categories = await _categoryRepository.GetAllAsync();
            PaymentMethods = await _paymentMethodRepository.GetAllAsync();
            OnPropertyChanged(nameof(Categories));
            OnPropertyChanged(nameof(PaymentMethods));
            await LoadRulesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load recurring subscriptions.");
            FormError = Strings.RecurringLoadFailed;
        }
    }

    private async Task LoadRulesAsync()
    {
        IReadOnlyList<RecurringRule> rules = await _repository.GetAllAsync();
        Rules.Clear();
        foreach (RecurringRule rule in rules)
        {
            Rules.Add(rule);
        }

        HasLoaded = true;
    }

    private void ResetForm()
    {
        Name = string.Empty;
        Amount = 0;
        Currency = Currency.DOP;
        Frequency = "Monthly";
        RuleType = "Subscription";
        DayOfMonth = _clock.Today.Day;
        Month = _clock.Today.Month;
        EndDate = null;
        SelectedCategory = null;
        SelectedPaymentMethod = null;
        FormError = string.Empty;
        OnPropertyChanged(nameof(NextChargeText));
    }

    [RelayCommand]
    private async Task PreviewGenerationAsync()
    {
        IsGenerating = true;
        GenerationError = string.Empty;
        try
        {
            Result result = await _generateUseCase.PreviewAsync();
            if (!result.IsSuccess)
            {
                GenerationError = result.Error?.Message ?? Strings.RecurringPreviewFailed;
                return;
            }
            PreviewItems.Clear();
            foreach (RecurringGenerationPreview item in (IReadOnlyList<RecurringGenerationPreview>)result.Value!)
            {
                PreviewItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to preview recurring generation.");
            GenerationError = Strings.RecurringPreviewFailed;
        }
        finally { IsGenerating = false; }
    }

    [RelayCommand]
    private async Task ConfirmGenerationAsync()
    {
        IsGenerating = true;
        GenerationError = string.Empty;
        try
        {
            Result result = await _generateUseCase.ExecuteAsync();
            if (!result.IsSuccess)
            {
                GenerationError = result.Error?.Message ?? Strings.RecurringGenerateFailed;
            }
            else
            {
                StatusMessage = string.Format(CultureInfo.CurrentUICulture, Strings.RecurringGenerated, result.Value);
            }

            PreviewItems.Clear();

            // Generating advances each rule's next charge: reload so the list shows what is stored.
            await LoadRulesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to confirm recurring generation.");
            GenerationError = Strings.RecurringGenerateFailed;
        }
        finally { IsGenerating = false; }
    }

    [RelayCommand]
    private async Task DeactivateSelectedAsync()
    {
        if (SelectedRule is null)
        {
            return;
        }

        Result result = await _deactivateUseCase.ExecuteAsync(SelectedRule.Id);
        if (!result.IsSuccess) { FormError = result.Error?.Message ?? Strings.RecurringDeactivateFailed; return; }
        StatusMessage = Strings.RecurringDeactivated;
        await LoadRulesAsync();
    }

    /// <summary>Skips the next charge of the rule whose row was clicked (never the rule being edited, if another).</summary>
    /// <param name="rule">The rule of the clicked row.</param>
    [RelayCommand]
    private async Task SkipAsync(RecurringRule? rule)
    {
        if (rule is null)
        {
            return;
        }

        Result result = await _skipUseCase.ExecuteAsync(rule.Id);
        if (!result.IsSuccess) { FormError = result.Error?.Message ?? Strings.RecurringSkipFailed; return; }
        StatusMessage = Strings.RecurringSkipped;
        await LoadRulesAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedRule is null)
        {
            return;
        }

        Result result = await _deleteUseCase.ExecuteAsync(SelectedRule.Id);
        if (!result.IsSuccess) { FormError = result.Error?.Message ?? Strings.RecurringDeleteFailed; return; }
        StatusMessage = Strings.RecurringDeleted;
        SelectedRule = null;
        await LoadRulesAsync();
    }
}
