using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Budgets.CreateOrUpdateBudget;
using ControlDeFinanzasIA.Application.UseCases.Budgets.DeleteBudget;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetsForMonth;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.UI.Formatting;
using ControlDeFinanzasIA.UI.Resources;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Management screen for monthly budgets per category (F-010, slice 3,
/// R-1): create, edit and delete a budget amount for a category in a
/// navigable month. Follows the <see cref="DebtsViewModel"/> template —
/// fire-and-forget constructor load, flat observable form fields, an
/// observable row list, and <c>Result</c>-driven error surfacing that
/// never throws into the UI (D-10).
/// </summary>
public sealed partial class BudgetsViewModel : ViewModelBase
{
    private readonly GetBudgetsForMonthQuery _getBudgetsForMonthQuery;
    private readonly CreateOrUpdateBudgetUseCase _createOrUpdateUseCase;
    private readonly DeleteBudgetUseCase _deleteUseCase;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IClock _clock;
    private readonly ILogger<BudgetsViewModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetsViewModel"/>
    /// class. Starts on the current month (<see cref="IClock.Today"/>)
    /// and kicks off the initial load, following the same
    /// constructor-load pattern as <see cref="DebtsViewModel"/> and
    /// <see cref="CalendarViewModel"/>.
    /// </summary>
    public BudgetsViewModel(
        GetBudgetsForMonthQuery getBudgetsForMonthQuery,
        CreateOrUpdateBudgetUseCase createOrUpdateUseCase,
        DeleteBudgetUseCase deleteUseCase,
        ICategoryRepository categoryRepository,
        IClock clock,
        ILogger<BudgetsViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(getBudgetsForMonthQuery);
        ArgumentNullException.ThrowIfNull(createOrUpdateUseCase);
        ArgumentNullException.ThrowIfNull(deleteUseCase);
        ArgumentNullException.ThrowIfNull(categoryRepository);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _getBudgetsForMonthQuery = getBudgetsForMonthQuery;
        _createOrUpdateUseCase = createOrUpdateUseCase;
        _deleteUseCase = deleteUseCase;
        _categoryRepository = categoryRepository;
        _clock = clock;
        _logger = logger;

        _selectedMonth = MonthPeriod.Anchor(_clock.Today);
        _selectedMonthLabel = FormatMonthLabel(_selectedMonth);
        Currencies = Enum.GetValues<Currency>();

        _ = LoadAsync();
    }

    /// <summary>Gets or sets the currently navigated month (always a month anchor, i.e. day 1).</summary>
    [ObservableProperty]
    private DateOnly _selectedMonth;

    /// <summary>Gets or sets the localized "MMMM yyyy" label of <see cref="SelectedMonth"/> (R-9).</summary>
    [ObservableProperty]
    private string _selectedMonthLabel;

    /// <summary>Gets or sets the category selected in the form.</summary>
    [ObservableProperty]
    private Guid? _selectedCategoryId;

    /// <summary>Gets or sets the budgeted amount entered in the form.</summary>
    [ObservableProperty]
    private decimal _amount;

    /// <summary>Gets or sets the currency selected in the form.</summary>
    [ObservableProperty]
    private Currency _currency = Currency.DOP;

    /// <summary>Gets or sets the error message shown when a save or delete operation fails.</summary>
    [ObservableProperty]
    private string _formError = string.Empty;

    /// <summary>Gets or sets the status message shown after a successful operation.</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Gets or sets a value indicating whether a load is in progress.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Gets or sets the budget row currently selected for editing. Setting
    /// this hydrates the form fields via <see cref="OnSelectedBudgetChanged"/>.
    /// </summary>
    [ObservableProperty]
    private BudgetRow? _selectedBudget;

    /// <summary>Gets the budget rows for <see cref="SelectedMonth"/>.</summary>
    public ObservableCollection<BudgetRow> Budgets { get; } = [];

    /// <summary>Gets the categories available in the category picker.</summary>
    public IReadOnlyList<Category> Categories { get; private set; } = [];

    /// <summary>Gets the currencies available in the currency picker.</summary>
    public IReadOnlyList<Currency> Currencies { get; }

    partial void OnSelectedBudgetChanged(BudgetRow? value)
    {
        FormError = string.Empty;

        if (value is null)
        {
            return;
        }

        SelectedCategoryId = value.CategoryId;
        Amount = value.Amount;
        Currency = value.Currency;
    }

    /// <summary>
    /// Selects a row for editing, hydrating the form with its values.
    /// </summary>
    /// <param name="row">The row to edit. Null is treated as no-op.</param>
    [RelayCommand]
    private void Edit(BudgetRow? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedBudget = row;
    }

    /// <summary>
    /// Upserts the budget described by the current form values for
    /// <see cref="SelectedMonth"/> (R-1, R-2).
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        FormError = string.Empty;

        var command = new CreateOrUpdateBudgetCommand(
            SelectedMonth.Year,
            SelectedMonth.Month,
            SelectedCategoryId,
            Amount,
            Currency);

        Result result = await _createOrUpdateUseCase.ExecuteAsync(command).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            FormError = result.Error?.Message ?? Strings.BudgetsErrorMessage;
            return;
        }

        StatusMessage = Strings.BudgetsSavedMessage;
        ResetForm();
        await LoadBudgetsAsync().ConfigureAwait(false);
    }

    /// <summary>Deletes the given budget row (hard delete, R-1).</summary>
    /// <param name="row">The row to delete. Null is treated as no-op.</param>
    [RelayCommand]
    private async Task DeleteAsync(BudgetRow? row)
    {
        if (row is null)
        {
            return;
        }

        FormError = string.Empty;
        Result result = await _deleteUseCase.ExecuteAsync(row.Id).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            FormError = result.Error?.Message ?? Strings.BudgetsErrorMessage;
            return;
        }

        StatusMessage = Strings.BudgetsDeletedMessage;
        if (SelectedBudget?.Id == row.Id)
        {
            ResetForm();
        }

        await LoadBudgetsAsync().ConfigureAwait(false);
    }

    /// <summary>Navigates to the month immediately before <see cref="SelectedMonth"/> and reloads.</summary>
    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        SelectedMonth = MonthPeriod.Previous(SelectedMonth);
        SelectedMonthLabel = FormatMonthLabel(SelectedMonth);
        await LoadBudgetsAsync().ConfigureAwait(false);
    }

    /// <summary>Navigates to the month immediately after <see cref="SelectedMonth"/> and reloads.</summary>
    [RelayCommand]
    private async Task NextMonthAsync()
    {
        SelectedMonth = MonthPeriod.Next(SelectedMonth);
        SelectedMonthLabel = FormatMonthLabel(SelectedMonth);
        await LoadBudgetsAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        FormError = string.Empty;

        try
        {
            Categories = await _categoryRepository.GetAllAsync().ConfigureAwait(false);
            OnPropertyChanged(nameof(Categories));

            await LoadBudgetsCoreAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load budgets.");
            FormError = Strings.BudgetsLoadErrorMessage;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadBudgetsAsync()
    {
        try
        {
            await LoadBudgetsCoreAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load budgets for {Month}.", SelectedMonth);
            FormError = Strings.BudgetsLoadErrorMessage;
        }
    }

    private async Task LoadBudgetsCoreAsync()
    {
        Result result = await _getBudgetsForMonthQuery
            .ExecuteAsync(SelectedMonth.Year, SelectedMonth.Month)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            FormError = result.Error?.Message ?? Strings.BudgetsLoadErrorMessage;
            return;
        }

        var categoryNames = Categories.ToDictionary(c => c.Id, c => c.Name);

        Budgets.Clear();
        foreach (Budget budget in (IReadOnlyList<Budget>)result.Value!)
        {
            Guid categoryId = budget.CategoryId!.Value;
            string categoryName = categoryNames.TryGetValue(categoryId, out string? name)
                ? name
                : string.Empty;

            Budgets.Add(new BudgetRow(budget.Id, categoryId, categoryName, budget.Amount.Amount, budget.Currency));
        }
    }

    private void ResetForm()
    {
        SelectedBudget = null;
        SelectedCategoryId = null;
        Amount = 0;
        Currency = Currency.DOP;
        FormError = string.Empty;
    }

    private static string FormatMonthLabel(DateOnly month)
        => UiCulture.MonthYear(month);
}
