using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.DetectAnomalies;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetDashboardInsights;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetInsights;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthEndForecast;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthExpenses;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthlyEvolution;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Application.UseCases.Exports.Common;
using ControlDeFinanzasIA.Application.UseCases.Exports.ExportMonthSummaryReport;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Forecasting;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.UI.Formatting;
using ControlDeFinanzasIA.UI.Navigation;
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
    private readonly ExportMonthSummaryReportUseCase _exportMonthSummary;
    private readonly IFileSaveDialog _fileSaveDialog;
    private readonly GetMonthExpensesQuery _getMonthExpensesQuery;
    private readonly INavigationService _navigation;

    private int _loadGeneration;
    private CancellationTokenSource? _narrationCts;

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
        ILogger<DashboardViewModel> logger,
        ExportMonthSummaryReportUseCase exportMonthSummary,
        IFileSaveDialog fileSaveDialog,
        GetMonthExpensesQuery getMonthExpensesQuery,
        INavigationService navigation)
    {
        ArgumentNullException.ThrowIfNull(getMonthSummaryQuery);
        ArgumentNullException.ThrowIfNull(getCategoryBreakdownQuery);
        ArgumentNullException.ThrowIfNull(getMonthlyEvolutionQuery);
        ArgumentNullException.ThrowIfNull(getUpcomingPaymentsQuery);
        ArgumentNullException.ThrowIfNull(getBudgetVsActualQuery);
        ArgumentNullException.ThrowIfNull(getDashboardInsightsQuery);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(exportMonthSummary);
        ArgumentNullException.ThrowIfNull(fileSaveDialog);
        ArgumentNullException.ThrowIfNull(getMonthExpensesQuery);
        ArgumentNullException.ThrowIfNull(navigation);

        _getMonthExpensesQuery = getMonthExpensesQuery;
        _navigation = navigation;
        _exportMonthSummary = exportMonthSummary;
        _fileSaveDialog = fileSaveDialog;
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

    /// <summary>
    /// Gets the latest expenses of <see cref="SelectedMonth"/>, most recent first, capped at
    /// <see cref="DashboardDefaults.MonthExpensesLimit"/>. Rebuilt on every <see cref="LoadAsync"/>, so
    /// it follows the month navigation.
    /// </summary>
    public ObservableCollection<MonthExpenseRow> MonthExpenses { get; } = [];

    /// <summary>Gets or sets the "N expenses in {month}" line, or empty when the list is not loaded.</summary>
    [ObservableProperty]
    private string _monthExpensesCountText = string.Empty;

    /// <summary>Gets or sets the "showing the latest N" note, or empty when every expense of the month is listed.</summary>
    [ObservableProperty]
    private string _monthExpensesShowingText = string.Empty;

    /// <summary>Gets or sets a value indicating whether the list loaded and has at least one expense.</summary>
    [ObservableProperty]
    private bool _hasMonthExpenses;

    /// <summary>
    /// Gets or sets a value indicating whether the list loaded successfully and the month has no expenses.
    /// It is <see langword="false"/> before the first load and after a failed load, so a failure never
    /// reads as "no expenses".
    /// </summary>
    [ObservableProperty]
    private bool _isMonthExpensesEmpty;

    /// <summary>Opens the Expenses screen filtered to the displayed month.</summary>
    [RelayCommand]
    private void ViewAllExpenses()
    {
        DateRange range = MonthPeriod.Range(SelectedMonth);
        _navigation.NavigateTo<ExpensesViewModel>(
            RouteNames.Expenses,
            vm => vm.ApplyDateRangeFilter(range.Start, range.End));
    }

    private void ApplyMonthExpenses(Result result)
    {
        ClearMonthExpenses();

        if (!result.IsSuccess)
        {
            LoadError = Strings.DashboardLoadErrorMessage;
            _logger.LogWarning(
                "Unable to load the month's expenses for {Month}: {ErrorCode} {ErrorMessage}",
                SelectedMonth,
                result.Error?.Code,
                result.Error?.Message);
            return;
        }

        var month = (MonthExpensesResult)result.Value!;
        foreach (MonthExpenseItem item in month.Items)
        {
            MonthExpenses.Add(ToMonthExpenseRow(item));
        }

        HasMissingRates |= month.HasMissingRates;
        HasMonthExpenses = month.Items.Count > 0;
        IsMonthExpensesEmpty = month.TotalCount == 0;
        MonthExpensesCountText = month.TotalCount switch
        {
            0 => string.Empty,
            1 => string.Format(CultureInfo.CurrentCulture, Strings.DashboardMonthExpensesCountOneFormat, SelectedMonthLabel),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.DashboardMonthExpensesCountFormat, month.TotalCount, SelectedMonthLabel),
        };
        MonthExpensesShowingText = month.TotalCount > month.Items.Count
            ? string.Format(CultureInfo.CurrentCulture, Strings.DashboardMonthExpensesShowingFormat, month.Items.Count)
            : string.Empty;
    }

    private void ClearMonthExpenses()
    {
        MonthExpenses.Clear();
        HasMonthExpenses = false;
        IsMonthExpensesEmpty = false;
        MonthExpensesCountText = string.Empty;
        MonthExpensesShowingText = string.Empty;
    }

    private static MonthExpenseRow ToMonthExpenseRow(MonthExpenseItem item) =>
        new(
            item.Date.ToString("d MMM", CultureInfo.CurrentCulture),
            item.Description,
            item.CategoryName,
            $"{item.Amount.Amount:N2} {item.Amount.Currency}",
            item.Amount.Currency != Domain.Enums.Currency.DOP && item.AmountDop is decimal dop
                ? FormatDop(dop)
                : string.Empty);
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

    /// <summary>Gets or sets a value indicating whether the narration pass (insights + anomaly narration) is in flight (D-12, S8b).</summary>
    [ObservableProperty]
    private bool _isInsightsLoading;

    /// <summary>Gets or sets the AI-narrated insights text (2-4 sentences), or empty when not <see cref="NarrativeStatus.Generated"/>.</summary>
    [ObservableProperty]
    private string _insightsText = string.Empty;

    /// <summary>Gets or sets the narrative-only fallback caption for the insights card ("IA no configurada"/"IA no disponible"), empty when narration succeeded.</summary>
    [ObservableProperty]
    private string _insightsStatusText = string.Empty;

    /// <summary>Gets or sets the narrative-only fallback caption shown under the anomalies list when batch narration did not run/succeed and the list is non-empty (R-6).</summary>
    [ObservableProperty]
    private string _anomalyNarrationStatusText = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether a report export is in flight. Shared by both export
    /// commands: it is the single source of truth for the double-invocation guard (F-018, D-11).
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportExcelCommand))]
    private bool _isExporting;

    /// <summary>Gets or sets the outcome text of the last export (file name on success, error text on failure); empty after a cancel.</summary>
    [ObservableProperty]
    private string _exportStatusMessage = string.Empty;

    /// <summary>Gets or sets a value indicating whether <see cref="ExportStatusMessage"/> describes a failure.</summary>
    [ObservableProperty]
    private bool _isExportError;

    /// <summary>Exports the displayed month as a PDF report.</summary>
    [RelayCommand(CanExecute = nameof(CanExport), AllowConcurrentExecutions = true)]
    private Task ExportPdfAsync() => ExportAsync(ReportFormat.Pdf);

    /// <summary>Exports the displayed month as an Excel workbook.</summary>
    [RelayCommand(CanExecute = nameof(CanExport), AllowConcurrentExecutions = true)]
    private Task ExportExcelAsync() => ExportAsync(ReportFormat.Xlsx);

    private bool CanExport() => !IsExporting;

    /// <summary>
    /// Maps a use-case failure code to its localized message; unknown or missing codes fall back to
    /// the generic export failure text.
    /// </summary>
    internal static string ExportErrorMessage(string? code) => code switch
    {
        ReportErrorCodes.RenderFailed => Strings.ExportRenderFailedMessage,
        ReportErrorCodes.SaveFailed => Strings.ExportSaveFailedMessage,
        ReportErrorCodes.DataFailed => Strings.ExportDataFailedMessage,
        ReportErrorCodes.WriterUnavailable => Strings.ExportWriterUnavailableMessage,
        _ => Strings.ExportFailedMessage,
    };

    private async Task ExportAsync(ReportFormat format)
    {
        // The real guard: ExecuteAsync does not consult CanExecute, and the two commands have
        // independent single-flight state, so the shared flag is checked before the first await.
        if (IsExporting)
        {
            return;
        }

        IsExporting = true;
        ExportStatusMessage = string.Empty;
        IsExportError = false;

        // Captured up front: navigating months while the dialog is open must not retarget the export.
        DateOnly month = SelectedMonth;

        try
        {
            using SaveTarget? target = await _fileSaveDialog.PickAsync(SuggestedFileName(month, format), format);
            if (target is null)
            {
                // Cancelled: nothing is written and no message is shown.
                return;
            }

            Result result = await _exportMonthSummary.ExecuteAsync(
                new ExportMonthSummaryReportCommand(month, format, ReportLabelsFactory.Create()),
                target);

            if (result.IsSuccess)
            {
                ExportStatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.ExportSucceededMessage, target.DisplayName);
            }
            else
            {
                IsExportError = true;
                ExportStatusMessage = ExportErrorMessage(result.Error?.Code);
                _logger.LogWarning(
                    "Month-summary export failed for {Month}: {ErrorCode} {ErrorMessage}",
                    month,
                    result.Error?.Code,
                    result.Error?.Message);
            }
        }
        catch (OperationCanceledException)
        {
            // Not a failure: no message.
        }
        catch (Exception ex)
        {
            IsExportError = true;
            ExportStatusMessage = Strings.ExportFailedMessage;
            _logger.LogError(ex, "Unable to export the month summary for {Month}.", month);
        }
        finally
        {
            IsExporting = false;
        }
    }

    private static string SuggestedFileName(DateOnly month, ReportFormat format)
    {
        string extension = format == ReportFormat.Pdf ? ".pdf" : ".xlsx";
        return string.Format(CultureInfo.CurrentCulture, Strings.ExportMonthSummaryFileName, month.ToString("yyyy-MM", CultureInfo.InvariantCulture)) + extension;
    }

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

        _narrationCts?.Cancel();
        _narrationCts?.Dispose();
        _narrationCts = new CancellationTokenSource();
        CancellationToken narrationToken = _narrationCts.Token;

        IsLoading = true;
        LoadError = string.Empty;
        HasMissingRates = false;
        SectionErrorMessage = string.Empty;
        InsightsText = string.Empty;
        InsightsStatusText = string.Empty;
        AnomalyNarrationStatusText = string.Empty;
        IsInsightsLoading = false;
        SelectedMonthLabel = FormatMonthLabel(SelectedMonth);

        try
        {
            Task<Result> summaryTask = _getMonthSummaryQuery.ExecuteAsync(SelectedMonth);
            Task<Result> breakdownTask = _getCategoryBreakdownQuery.ExecuteAsync(SelectedMonth);
            Task<Result> evolutionTask = _getMonthlyEvolutionQuery.ExecuteAsync(SelectedMonth);
            Task<Result> budgetVsActualTask = _getBudgetVsActualQuery.ExecuteAsync(SelectedMonth);
            Task<Result> monthExpensesTask = _getMonthExpensesQuery.ExecuteAsync(SelectedMonth);

            await Task.WhenAll(summaryTask, breakdownTask, evolutionTask, budgetVsActualTask, monthExpensesTask);

            ApplySummary(summaryTask.Result);
            ApplyCategoryBreakdown(breakdownTask.Result);
            ApplyMonthlyEvolution(evolutionTask.Result);
            ApplyBudgetVsActual(budgetVsActualTask.Result);
            ApplyMonthExpenses(monthExpensesTask.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load the dashboard for {Month}.", SelectedMonth);
            LoadError = Strings.DashboardLoadErrorMessage;
            ClearMonthExpenses();
        }

        DashboardInsightsSnapshot? snapshot = await LoadDeterministicInsightsAsync(generation);

        IsLoading = false;

        if (snapshot is not null && generation == _loadGeneration)
        {
            _ = LoadNarrationAsync(snapshot, generation, narrationToken);
        }
    }

    /// <summary>
    /// Runs the deterministic forecast/anomalies step (<see cref="GetDashboardInsightsQuery.GetDeterministicAsync"/>),
    /// in its own <c>try/catch</c> so a forecast or detection failure sets
    /// <see cref="SectionErrorMessage"/> without ever touching <see cref="LoadError"/>
    /// or any Phase 8/10 section (R-3, R-9, D-12). Applies its result only
    /// when <paramref name="generation"/> is still the current load — a
    /// superseded (stale) load is dropped silently. Returns the computed
    /// snapshot (or <see langword="null"/> on failure/stale) so
    /// <see cref="LoadAsync"/> can hand it to the separate, non-blocking
    /// narration pass without recomputing it (R-7, D-12).
    /// </summary>
    private async Task<DashboardInsightsSnapshot?> LoadDeterministicInsightsAsync(int generation)
    {
        try
        {
            Result result = await _getDashboardInsightsQuery.GetDeterministicAsync(SelectedMonth);
            if (generation != _loadGeneration)
            {
                return null;
            }

            if (result.IsSuccess)
            {
                var snapshot = (DashboardInsightsSnapshot)result.Value!;
                ApplyDeterministicInsights(snapshot);
                return snapshot;
            }

            SectionErrorMessage = Strings.DashboardSectionErrorMessage;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load the forecast/anomalies sections for {Month}.", SelectedMonth);
            if (generation == _loadGeneration)
            {
                SectionErrorMessage = Strings.DashboardSectionErrorMessage;
            }

            return null;
        }
    }

    /// <summary>
    /// Runs the non-blocking narration pass (<see cref="GetDashboardInsightsQuery.GetNarrationAsync"/>)
    /// over an already-computed <paramref name="snapshot"/> (R-7, D-12).
    /// Mirrors <see cref="LoadUpcomingPaymentsAsync"/>'s "own try/catch,
    /// fire-and-forget" shape, but additionally guards every state mutation
    /// with the generation token: a superseded narration pass (the user
    /// navigated months before this one finished) is dropped silently,
    /// including a caller-initiated cancellation from a superseding
    /// <see cref="LoadAsync"/> call.
    /// </summary>
    private async Task LoadNarrationAsync(DashboardInsightsSnapshot snapshot, int generation, CancellationToken ct)
    {
        IsInsightsLoading = snapshot.Facts is not null;

        try
        {
            Result result = await _getDashboardInsightsQuery.GetNarrationAsync(snapshot, ct);
            if (generation != _loadGeneration)
            {
                return;
            }

            if (result.IsSuccess)
            {
                ApplyNarration((DashboardNarration)result.Value!);
            }
            else
            {
                ApplyNarrationFallback();
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer LoadAsync — silent, no LoadError/SectionErrorMessage (R-7).
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard narration failed for {Month}.", snapshot.Month);
            if (generation == _loadGeneration)
            {
                ApplyNarrationFallback();
            }
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                IsInsightsLoading = false;
            }
        }
    }

    /// <summary>
    /// Applies a <see cref="DashboardNarration"/> to the insights card and
    /// the anomaly rows (D-12). <see cref="Anomalies"/> is never rebuilt
    /// here — only rows already present get their <see cref="AnomalyListItem.Message"/>
    /// replaced in place, and a templated message is never cleared for an
    /// anomaly the parser could not resolve (spec S-6, R-6).
    /// </summary>
    private void ApplyNarration(DashboardNarration narration)
    {
        ApplyInsightsNarrative(narration.Insights);

        AnomalyNarrationStatusText = narration.Anomalies.Status is NarrativeStatus.NotConfigured or NarrativeStatus.Unavailable
            && Anomalies.Count > 0
            ? NarrativeStatusLabel(narration.Anomalies.Status)
            : string.Empty;

        foreach (AnomalyListItem item in Anomalies)
        {
            if (narration.Anomalies.MessagesByAlertId.TryGetValue(item.AlertId, out string? narratedMessage))
            {
                item.Message = narratedMessage;
                item.IsNarrated = true;
            }
        }
    }

    private void ApplyInsightsNarrative(InsightsNarrative insights)
    {
        InsightsText = insights.Status == NarrativeStatus.Generated ? insights.Text ?? string.Empty : string.Empty;
        InsightsStatusText = insights.Status switch
        {
            NarrativeStatus.NotConfigured => Strings.DashboardAiNotConfigured,
            NarrativeStatus.Unavailable => Strings.DashboardAiUnavailable,
            NarrativeStatus.Skipped => Strings.DashboardInsightsNoData,
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Applies the "AI unavailable" fallback to both narrative spots when
    /// the narration step itself fails unexpectedly (a defensive path
    /// beyond the success-shaped use cases, R-6). Never clears a templated
    /// anomaly <see cref="AnomalyListItem.Message"/>.
    /// </summary>
    private void ApplyNarrationFallback()
    {
        InsightsText = string.Empty;
        InsightsStatusText = Strings.DashboardAiUnavailable;
        AnomalyNarrationStatusText = Anomalies.Count > 0 ? Strings.DashboardAiUnavailable : string.Empty;
    }

    private static string NarrativeStatusLabel(NarrativeStatus status) => status switch
    {
        NarrativeStatus.NotConfigured => Strings.DashboardAiNotConfigured,
        NarrativeStatus.Unavailable => Strings.DashboardAiUnavailable,
        _ => string.Empty,
    };

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
