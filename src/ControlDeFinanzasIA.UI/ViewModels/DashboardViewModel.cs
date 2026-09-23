using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.DetectAnomalies;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetDashboardInsights;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthEndForecast;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthlyEvolution;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Forecasting;
using ControlDeFinanzasIA.UI.Formatting;
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
    private readonly GetBudgetVsActualQuery _getBudgetVsActualQuery;
    private readonly GetDashboardInsightsQuery _getDashboardInsightsQuery;
    private readonly IClock _clock;
    private readonly ILogger<DashboardViewModel> _logger;

    private int _loadGeneration;

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
        GetBudgetVsActualQuery getBudgetVsActualQuery,
        GetDashboardInsightsQuery getDashboardInsightsQuery,
        IClock clock,
        ILogger<DashboardViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(getMonthSummaryQuery);
        ArgumentNullException.ThrowIfNull(getCategoryBreakdownQuery);
        ArgumentNullException.ThrowIfNull(getMonthlyEvolutionQuery);
        ArgumentNullException.ThrowIfNull(getUpcomingPaymentsQuery);
        ArgumentNullException.ThrowIfNull(getBudgetVsActualQuery);
        ArgumentNullException.ThrowIfNull(getDashboardInsightsQuery);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _getMonthSummaryQuery = getMonthSummaryQuery;
        _getCategoryBreakdownQuery = getCategoryBreakdownQuery;
        _getMonthlyEvolutionQuery = getMonthlyEvolutionQuery;
        _getUpcomingPaymentsQuery = getUpcomingPaymentsQuery;
        _getBudgetVsActualQuery = getBudgetVsActualQuery;
        _getDashboardInsightsQuery = getDashboardInsightsQuery;
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

    /// <summary>
    /// Gets the budget-progress items (R-7): one bar per budgeted category
    /// for <see cref="SelectedMonth"/>, ordered by percent-consumed
    /// descending (indeterminate lines last), reusing the Dashboard's own
    /// month-navigation state — there is no second, independent month
    /// selector. Rebuilt on every <see cref="LoadAsync"/>, including
    /// <see cref="PreviousMonthCommand"/>/<see cref="NextMonthCommand"/>.
    /// </summary>
    public ObservableCollection<BudgetProgressItem> BudgetProgress { get; } = [];

    /// <summary>Gets or sets a value indicating whether a month-end forecast was computed for <see cref="SelectedMonth"/>.</summary>
    [ObservableProperty]
    private bool _hasForecast;

    /// <summary>Gets or sets the pre-formatted, DOP-normalized projected month-end total.</summary>
    [ObservableProperty]
    private string _forecastProjectedDisplay = string.Empty;

    /// <summary>Gets or sets the pre-formatted, DOP-normalized amount spent to date.</summary>
    [ObservableProperty]
    private string _forecastSpentToDateDisplay = string.Empty;

    /// <summary>Gets or sets the pre-formatted "{percent} of budget" line, or empty when no budget applies (S-8).</summary>
    [ObservableProperty]
    private string _forecastBudgetDisplay = string.Empty;

    /// <summary>Gets or sets the localized state message for a non-<see cref="ForecastState.Projected"/> forecast.</summary>
    [ObservableProperty]
    private string _forecastStateMessage = string.Empty;

    /// <summary>Gets or sets the budget status of the projected-vs-budget comparison, for the shared budget-progress color classes.</summary>
    [ObservableProperty]
    private BudgetStatus? _forecastBudgetStatus;

    /// <summary>Gets or sets the error message shown when the forecast or anomaly-detection section fails to load (R-3, D-12).</summary>
    [ObservableProperty]
    private string _sectionErrorMessage = string.Empty;

    /// <summary>
    /// Gets the active (unacknowledged) anomalies for <see cref="SelectedMonth"/>, in detector
    /// order (severity desc, then ratio-to-median desc). Rebuilt on every <see cref="LoadAsync"/>.
    /// </summary>
    public ObservableCollection<AnomalyListItem> Anomalies { get; } = [];

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
    /// Runs the four period-dependent queries concurrently
    /// (<see cref="Task.WhenAll(Task[])"/>) and rebuilds every bound
    /// collection/series from a single coherent snapshot, so a load never
    /// renders a summary from one month next to a chart (or budget line)
    /// from another (R-1, R-7). The budget-vs-actual query joins this
    /// batch — not <see cref="LoadUpcomingPaymentsAsync"/>'s load-once
    /// pattern — because the budget-progress section is anchored to
    /// <see cref="SelectedMonth"/>, not to today.
    /// </summary>
    private async Task LoadAsync()
    {
        int generation = ++_loadGeneration;

        IsLoading = true;
        LoadError = string.Empty;
        HasMissingRates = false;
        SectionErrorMessage = string.Empty;
        SelectedMonthLabel = FormatMonthLabel(SelectedMonth);

        try
        {
            Task<Result> summaryTask = _getMonthSummaryQuery.ExecuteAsync(SelectedMonth);
            Task<Result> breakdownTask = _getCategoryBreakdownQuery.ExecuteAsync(SelectedMonth);
            Task<Result> evolutionTask = _getMonthlyEvolutionQuery.ExecuteAsync(SelectedMonth);
            Task<Result> budgetVsActualTask = _getBudgetVsActualQuery.ExecuteAsync(SelectedMonth);

            await Task.WhenAll(summaryTask, breakdownTask, evolutionTask, budgetVsActualTask);

            ApplySummary(summaryTask.Result);
            ApplyCategoryBreakdown(breakdownTask.Result);
            ApplyMonthlyEvolution(evolutionTask.Result);
            ApplyBudgetVsActual(budgetVsActualTask.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load the dashboard for {Month}.", SelectedMonth);
            LoadError = Strings.DashboardLoadErrorMessage;
        }

        await LoadDeterministicInsightsAsync(generation);

        IsLoading = false;
    }

    /// <summary>
    /// Runs the deterministic forecast/anomalies step (<see cref="GetDashboardInsightsQuery.GetDeterministicAsync"/>),
    /// in its own <c>try/catch</c> so a forecast or detection failure sets
    /// <see cref="SectionErrorMessage"/> without ever touching <see cref="LoadError"/>
    /// or any Phase 8/10 section (R-3, R-9, D-12). Applies its result only
    /// when <paramref name="generation"/> is still the current load — a
    /// superseded (stale) load is dropped silently.
    /// </summary>
    private async Task LoadDeterministicInsightsAsync(int generation)
    {
        try
        {
            Result result = await _getDashboardInsightsQuery.GetDeterministicAsync(SelectedMonth);
            if (generation != _loadGeneration)
            {
                return;
            }

            if (result.IsSuccess)
            {
                ApplyDeterministicInsights((DashboardInsightsSnapshot)result.Value!);
            }
            else
            {
                SectionErrorMessage = Strings.DashboardSectionErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load the forecast/anomalies sections for {Month}.", SelectedMonth);
            if (generation == _loadGeneration)
            {
                SectionErrorMessage = Strings.DashboardSectionErrorMessage;
            }
        }
    }

    /// <summary>
    /// Applies a <see cref="DashboardInsightsSnapshot"/> to the forecast and
    /// anomalies sections. Each section is independent: a <see langword="null"/>
    /// <see cref="DashboardInsightsSnapshot.Forecast"/> or
    /// <see cref="DashboardInsightsSnapshot.Anomalies"/> (its step threw,
    /// already logged as Error by the composite) sets <see cref="SectionErrorMessage"/>
    /// without blanking the other section (D-12).
    /// </summary>
    private void ApplyDeterministicInsights(DashboardInsightsSnapshot snapshot)
    {
        bool sectionFailed = false;

        if (snapshot.Forecast is not null)
        {
            ApplyForecast(snapshot.Forecast);
        }
        else
        {
            HasForecast = false;
            sectionFailed = true;
        }

        Anomalies.Clear();
        if (snapshot.Anomalies is not null)
        {
            foreach (AnomalyItemDto item in snapshot.Anomalies)
            {
                Anomalies.Add(ToAnomalyListItem(item));
            }
        }
        else
        {
            sectionFailed = true;
        }

        SectionErrorMessage = sectionFailed ? Strings.DashboardSectionErrorMessage : string.Empty;
        HasMissingRates |= snapshot.HasMissingRates;
    }

    /// <summary>
    /// Projects a <see cref="MonthEndForecastDto"/> into UI-ready display
    /// text (D-12): <see cref="ForecastStateMessage"/> carries the
    /// non-<see cref="ForecastState.Projected"/> explanation, and
    /// <see cref="ForecastBudgetDisplay"/> stays empty when no budget
    /// applies (S-8) or the projection is not numeric.
    /// </summary>
    private void ApplyForecast(MonthEndForecastDto forecast)
    {
        HasForecast = true;
        ForecastSpentToDateDisplay = FormatDop(forecast.SpentToDateDop);
        ForecastBudgetStatus = forecast.ProjectedBudgetStatus;

        ForecastBudgetDisplay = forecast.BudgetTotalDop is not null && forecast.ProjectedPercentOfBudget is decimal percent
            ? string.Format(
                CultureInfo.CurrentCulture,
                Strings.DashboardForecastBudgetFormat,
                $"{percent.ToString("0.0", CultureInfo.CurrentCulture)}%")
            : string.Empty;

        ForecastProjectedDisplay = forecast.ProjectedDop is decimal projected ? FormatDop(projected) : string.Empty;

        ForecastStateMessage = forecast.State switch
        {
            ForecastState.InsufficientData => Strings.DashboardForecastInsufficientData,
            ForecastState.ClosedMonth => Strings.DashboardForecastClosedMonth,
            ForecastState.FutureMonth => Strings.DashboardForecastFutureMonth,
            _ => string.Empty,
        };
    }

    /// <summary>Maps an <see cref="AnomalyItemDto"/> into its display row, with the deterministic template as the initial <see cref="AnomalyListItem.Message"/>.</summary>
    private static AnomalyListItem ToAnomalyListItem(AnomalyItemDto item) =>
        new(
            item.AlertId,
            item.CategoryName,
            item.Description,
            FormatDop(item.AmountDop),
            item.Date.ToString("d MMM", CultureInfo.CurrentCulture),
            item.Severity,
            SeverityLabel(item.Severity),
            item.Message);

    private static string SeverityLabel(AnomalySeverity severity) => severity switch
    {
        AnomalySeverity.Info => Strings.DashboardAnomalySeverityInfo,
        AnomalySeverity.Warning => Strings.DashboardAnomalySeverityWarning,
        AnomalySeverity.Critical => Strings.DashboardAnomalySeverityCritical,
        _ => string.Empty,
    };

    /// <summary>
    /// Dismisses (acknowledges) a single anomaly row (R-9: the only
    /// discard action in this phase). On success the row is removed from
    /// <see cref="Anomalies"/>; on failure the row stays and the failure
    /// is logged only — never surfaced as <see cref="LoadError"/> or
    /// <see cref="SectionErrorMessage"/> (D-12).
    /// </summary>
    [RelayCommand]
    private async Task DismissAnomalyAsync(AnomalyListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        try
        {
            Result result = await _getDashboardInsightsQuery.DismissAnomalyAsync(item.AlertId);
            if (result.IsSuccess)
            {
                Anomalies.Remove(item);
            }
            else
            {
                _logger.LogWarning(
                    "Unable to dismiss anomaly {AlertId}: {ErrorCode} {ErrorMessage}",
                    item.AlertId,
                    result.Error?.Code,
                    result.Error?.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to dismiss anomaly {AlertId}.", item.AlertId);
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
    /// Rebuilds <see cref="BudgetProgress"/> from a <see cref="GetBudgetVsActualQuery"/>
    /// result (R-7, D-11). A category with no budget set for <see cref="SelectedMonth"/>
    /// never appears here, because the query iterates budgets, not categories.
    /// </summary>
    private void ApplyBudgetVsActual(Result result)
    {
        BudgetProgress.Clear();

        if (!result.IsSuccess)
        {
            LoadError = result.Error?.Message ?? "No se pudo cargar el progreso de presupuestos.";
            return;
        }

        var budgetVsActual = (BudgetVsActualResult)result.Value!;
        foreach (BudgetVsActualLine line in budgetVsActual.Lines)
        {
            BudgetProgress.Add(ToBudgetProgressItem(line));
        }

        HasMissingRates |= budgetVsActual.HasMissingRates;
    }

    /// <summary>
    /// Projects a <see cref="BudgetVsActualLine"/> into UI-ready display
    /// text (R-5, S-6): an indeterminate line (missing conversion rate on
    /// either side) NEVER renders as 0% or 100% — its <see cref="BudgetProgressItem.PercentDisplay"/>
    /// is the localized <see cref="Strings.DashboardBudgetIndeterminateLabel"/>
    /// placeholder and its <see cref="BudgetProgressItem.Status"/> stays
    /// <see langword="null"/>, so no color class applies.
    /// </summary>
    private static BudgetProgressItem ToBudgetProgressItem(BudgetVsActualLine line)
    {
        string amountsDisplay = string.Format(
            CultureInfo.CurrentCulture,
            "{0} / {1}",
            FormatDopOrPlaceholder(line.ActualDop),
            FormatDopOrPlaceholder(line.BudgetDop));

        if (line.IsIndeterminate)
        {
            return new BudgetProgressItem(
                line.CategoryId,
                line.CategoryName,
                amountsDisplay,
                Strings.DashboardBudgetIndeterminateLabel,
                ProgressValue: 0d,
                Status: null);
        }

        decimal percent = line.PercentConsumed!.Value;
        return new BudgetProgressItem(
            line.CategoryId,
            line.CategoryName,
            amountsDisplay,
            PercentDisplay: $"{percent.ToString("0.0", CultureInfo.CurrentCulture)}%",
            ProgressValue: Math.Min((double)percent, 100d),
            Status: line.Status);
    }

    /// <summary>Formats a DOP amount, or an em dash placeholder when the side failed to convert (R-5, S-6).</summary>
    private static string FormatDopOrPlaceholder(decimal? amount)
        => amount.HasValue ? FormatDop(amount.Value) : "—";

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
    /// are combined into one total. Delegates to <see cref="MoneyFormat.ApproximateDop"/>
    /// (D-5, Phase 9), which now owns the shared formatting body.
    /// </summary>
    private static string FormatDop(decimal amount) => MoneyFormat.ApproximateDop(amount);
}
