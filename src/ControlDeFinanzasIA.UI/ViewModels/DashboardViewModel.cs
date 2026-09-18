using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthlyEvolution;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.UI.Resources;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// View-model for the Dashboard screen: monthly period navigation, the
/// DOP-normalized summary card, the category-breakdown and
/// monthly-evolution charts (F-008, slice 2), and the "Próximos pagos"
/// card (F-008, slice 3).
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly GetMonthSummaryQuery _getMonthSummaryQuery;
    private readonly GetCategoryBreakdownQuery _getCategoryBreakdownQuery;
    private readonly GetMonthlyEvolutionQuery _getMonthlyEvolutionQuery;
    private readonly GetUpcomingPaymentsQuery _getUpcomingPaymentsQuery;
    private readonly IClock _clock;
    private readonly ILogger<DashboardViewModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardViewModel"/> class.
    /// Starts on the current month (<see cref="IClock.Today"/>) and kicks off
    /// the initial load, following the same constructor-load pattern as
    /// <see cref="RecurringRulesViewModel"/>. "Próximos pagos" is loaded
    /// separately from <see cref="LoadAsync"/>, exactly once, because it is
    /// anchored to today rather than to <see cref="SelectedMonth"/> (R-1a,
    /// D-6): it must not re-run when the user navigates months.
    /// </summary>
    public DashboardViewModel(
        GetMonthSummaryQuery getMonthSummaryQuery,
        GetCategoryBreakdownQuery getCategoryBreakdownQuery,
        GetMonthlyEvolutionQuery getMonthlyEvolutionQuery,
        GetUpcomingPaymentsQuery getUpcomingPaymentsQuery,
        IClock clock,
        ILogger<DashboardViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(getMonthSummaryQuery);
        ArgumentNullException.ThrowIfNull(getCategoryBreakdownQuery);
        ArgumentNullException.ThrowIfNull(getMonthlyEvolutionQuery);
        ArgumentNullException.ThrowIfNull(getUpcomingPaymentsQuery);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _getMonthSummaryQuery = getMonthSummaryQuery;
        _getCategoryBreakdownQuery = getCategoryBreakdownQuery;
        _getMonthlyEvolutionQuery = getMonthlyEvolutionQuery;
        _getUpcomingPaymentsQuery = getUpcomingPaymentsQuery;
        _clock = clock;
        _logger = logger;

        _selectedMonth = MonthPeriod.Anchor(_clock.Today);
        _selectedMonthLabel = FormatMonthLabel(_selectedMonth);

        _ = LoadAsync();
        _ = LoadUpcomingPaymentsAsync();
    }

    /// <summary>Gets or sets the currently navigated month (always a month anchor, i.e. day 1).</summary>
    [ObservableProperty]
    private DateOnly _selectedMonth;

    /// <summary>Gets or sets the localized "MMMM yyyy" label of <see cref="SelectedMonth"/>.</summary>
    [ObservableProperty]
    private string _selectedMonthLabel;

    /// <summary>Gets or sets a value indicating whether a load is in progress.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Gets or sets the error message shown when a load fails.</summary>
    [ObservableProperty]
    private string _loadError = string.Empty;

    /// <summary>Gets or sets the pre-formatted, DOP-normalized total expenses for <see cref="SelectedMonth"/>.</summary>
    [ObservableProperty]
    private string _totalExpensesDisplay = string.Empty;

    /// <summary>Gets or sets the pre-formatted, DOP-normalized total incomes for <see cref="SelectedMonth"/>.</summary>
    [ObservableProperty]
    private string _totalIncomesDisplay = string.Empty;

    /// <summary>Gets or sets the pre-formatted, DOP-normalized net balance for <see cref="SelectedMonth"/>.</summary>
    [ObservableProperty]
    private string _netBalanceDisplay = string.Empty;

    /// <summary>
    /// Gets or sets the pre-formatted MoM expenses delta (e.g. "+12.5%"), or
    /// an empty string when the previous month had no expenses (R-2).
    /// </summary>
    [ObservableProperty]
    private string _expensesDeltaDisplay = string.Empty;

    /// <summary>Gets or sets a value indicating whether <see cref="ExpensesDeltaDisplay"/> represents a negative delta.</summary>
    [ObservableProperty]
    private bool _isExpensesDeltaNegative;

    /// <summary>
    /// Gets or sets a value indicating whether any currency bucket across the
    /// summary, category-breakdown or monthly-evolution widgets failed to
    /// convert to DOP (R-7, S-6). Drives the "tasa no disponible" notice.
    /// </summary>
    [ObservableProperty]
    private bool _hasMissingRates;

    /// <summary>Gets or sets the LiveCharts series backing the category-breakdown chart.</summary>
    [ObservableProperty]
    private ISeries[] _categorySeries = [];

    /// <summary>Gets or sets the LiveCharts series backing the monthly-evolution chart.</summary>
    [ObservableProperty]
    private ISeries[] _evolutionSeries = [];

    /// <summary>Gets or sets the LiveCharts X axes (month labels) backing the monthly-evolution chart.</summary>
    [ObservableProperty]
    private Axis[] _evolutionXAxes = [];

    /// <summary>
    /// Gets the top-N + "Otras" category slices for <see cref="SelectedMonth"/>.
    /// Asserted directly by unit tests; <see cref="CategorySeries"/> is derived from it.
    /// </summary>
    public ObservableCollection<CategorySlice> CategorySlices { get; } = [];

    /// <summary>
    /// Gets the exactly-6, zero-filled monthly points ending at
    /// <see cref="SelectedMonth"/>. Asserted directly by unit tests;
    /// <see cref="EvolutionSeries"/>/<see cref="EvolutionXAxes"/> are derived from it.
    /// </summary>
    public ObservableCollection<EvolutionPoint> EvolutionPoints { get; } = [];

    /// <summary>
    /// Gets the "Próximos pagos" items (R-6): pending debt installments and
    /// upcoming recurring-rule generations within the next
    /// <see cref="Application.Common.DashboardDefaults.UpcomingHorizonDays"/>
    /// days from <see cref="IClock.Today"/>, date-ordered. Loaded exactly
    /// once (see <see cref="LoadUpcomingPaymentsAsync"/>): navigating months
    /// via <see cref="PreviousMonthCommand"/>/<see cref="NextMonthCommand"/>
    /// never touches this collection (R-1a, D-6).
    /// </summary>
    public ObservableCollection<UpcomingPaymentItem> UpcomingPayments { get; } = [];

    /// <summary>Navigates to the month immediately before <see cref="SelectedMonth"/> and reloads.</summary>
    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        SelectedMonth = MonthPeriod.Previous(SelectedMonth);
        await LoadAsync();
    }

    /// <summary>Navigates to the month immediately after <see cref="SelectedMonth"/> and reloads.</summary>
    [RelayCommand]
    private async Task NextMonthAsync()
    {
        SelectedMonth = MonthPeriod.Next(SelectedMonth);
        await LoadAsync();
    }

    /// <summary>Reloads the Dashboard for the current <see cref="SelectedMonth"/> without changing it.</summary>
    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    /// <summary>
    /// Runs the three period-dependent queries concurrently
    /// (<see cref="Task.WhenAll(Task[])"/>) and rebuilds every bound
    /// collection/series from a single coherent snapshot, so a load never
    /// renders a summary from one month next to a chart from another (R-1, R-7).
    /// </summary>
    private async Task LoadAsync()
    {
        IsLoading = true;
        LoadError = string.Empty;
        HasMissingRates = false;
        SelectedMonthLabel = FormatMonthLabel(SelectedMonth);

        try
        {
            Task<Result> summaryTask = _getMonthSummaryQuery.ExecuteAsync(SelectedMonth);
            Task<Result> breakdownTask = _getCategoryBreakdownQuery.ExecuteAsync(SelectedMonth);
            Task<Result> evolutionTask = _getMonthlyEvolutionQuery.ExecuteAsync(SelectedMonth);

            await Task.WhenAll(summaryTask, breakdownTask, evolutionTask);

            ApplySummary(summaryTask.Result);
            ApplyCategoryBreakdown(breakdownTask.Result);
            ApplyMonthlyEvolution(evolutionTask.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load the dashboard for {Month}.", SelectedMonth);
            LoadError = Strings.DashboardLoadErrorMessage;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplySummary(Result result)
    {
        if (!result.IsSuccess)
        {
            LoadError = result.Error?.Message ?? "No se pudo cargar el resumen del mes.";
            return;
        }

        var summary = (MonthSummaryResult)result.Value!;
        TotalExpensesDisplay = FormatDop(summary.ExpensesDop);
        TotalIncomesDisplay = FormatDop(summary.IncomesDop);
        NetBalanceDisplay = FormatDop(summary.NetBalanceDop);
        ExpensesDeltaDisplay = summary.ExpensesDeltaPercent is decimal delta
            ? $"{delta.ToString("+0.0;-0.0;0.0", CultureInfo.CurrentCulture)}%"
            : string.Empty;
        IsExpensesDeltaNegative = summary.ExpensesDeltaPercent is < 0;
        HasMissingRates |= summary.HasMissingRates;
    }

    private void ApplyCategoryBreakdown(Result result)
    {
        CategorySlices.Clear();

        if (!result.IsSuccess)
        {
            LoadError = result.Error?.Message ?? "No se pudo cargar el gráfico de categorías.";
            CategorySeries = [];
            return;
        }

        var breakdown = (CategoryBreakdownResult)result.Value!;
        foreach (CategorySlice slice in breakdown.Slices)
        {
            CategorySlices.Add(slice);
        }

        HasMissingRates |= breakdown.HasMissingRates;

        CategorySeries = breakdown.Slices
            .Select(slice => (ISeries)new PieSeries<double>
            {
                Values = [(double)slice.AmountDop],
                Name = slice.Label,
            })
            .ToArray();
    }

    private void ApplyMonthlyEvolution(Result result)
    {
        EvolutionPoints.Clear();

        if (!result.IsSuccess)
        {
            LoadError = result.Error?.Message ?? "No se pudo cargar el gráfico de evolución.";
            EvolutionSeries = [];
            EvolutionXAxes = [];
            return;
        }

        var evolution = (MonthlyEvolutionResult)result.Value!;
        foreach (EvolutionPoint point in evolution.Points)
        {
            EvolutionPoints.Add(point);
        }

        HasMissingRates |= evolution.HasMissingRates;

        EvolutionSeries =
        [
            new LineSeries<double>
            {
                Values = evolution.Points.Select(p => (double)p.ExpensesDop).ToArray(),
                Name = "Gastos",
            },
            new LineSeries<double>
            {
                Values = evolution.Points.Select(p => (double)p.IncomesDop).ToArray(),
                Name = "Ingresos",
            },
        ];

        EvolutionXAxes =
        [
            new Axis
            {
                Labels = evolution.Points
                    .Select(p => new DateOnly(p.Year, p.Month, 1).ToString("MMM yyyy", CultureInfo.CurrentCulture))
                    .ToArray(),
            },
        ];
    }

    /// <summary>
    /// Loads the "Próximos pagos" card exactly once (constructor-only call,
    /// see the class remarks and R-1a/D-6). Deliberately separate from
    /// <see cref="LoadAsync"/> so that <see cref="PreviousMonthAsync"/> and
    /// <see cref="NextMonthAsync"/> — which call <see cref="LoadAsync"/> on
    /// every navigation — never re-run this query.
    /// </summary>
    private async Task LoadUpcomingPaymentsAsync()
    {
        try
        {
            Result result = await _getUpcomingPaymentsQuery.ExecuteAsync();
            ApplyUpcomingPayments(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load upcoming payments for the dashboard.");
        }
    }

    private void ApplyUpcomingPayments(Result result)
    {
        UpcomingPayments.Clear();

        if (!result.IsSuccess)
        {
            return;
        }

        var items = (IReadOnlyList<UpcomingPaymentItem>)result.Value!;
        foreach (UpcomingPaymentItem item in items)
        {
            UpcomingPayments.Add(item);
        }
    }

    private static string FormatMonthLabel(DateOnly month)
        => month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);

    /// <summary>
    /// Formats a DOP amount with the app-wide "≈ {N2} {Currency}" pattern
    /// (<see cref="Strings.ApproximateFormat"/>), reused verbatim from
    /// <c>ExpensesViewModel.EnrichWithConversionAsync</c>. Every Dashboard
    /// summary figure is a normalized aggregate (R-2), so it always carries
    /// the "≈" prefix — unlike a per-transaction display, there is no
    /// single-currency "exact" case at this granularity once currencies
    /// are combined into one total.
    /// </summary>
    private static string FormatDop(decimal amount)
        => string.Format(CultureInfo.CurrentCulture, Strings.ApproximateFormat, $"{amount:N2} DOP");
}
