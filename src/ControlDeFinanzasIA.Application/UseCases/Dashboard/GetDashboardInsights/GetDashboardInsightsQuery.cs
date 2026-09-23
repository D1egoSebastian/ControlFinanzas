using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.AcknowledgeAnomaly;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.DetectAnomalies;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetAnomalyNarrative;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetInsights;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthEndForecast;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetDashboardInsights;

/// <summary>
/// Unica query compuesta nueva del dashboard (F-014, R-7, D-10). Expone
/// dos operaciones separadamente awaiteables: un paso deterministico
/// (pronostico + anomalias detectadas) que NUNCA resuelve un
/// <c>ILLMClient</c>, y un paso de narracion (insights + narracion de
/// anomalias por lotes) que hace a lo sumo 2 llamadas HTTP concurrentes
/// (D-11). El paso deterministico corre sus consultas
/// SECUENCIALMENTE -- no <c>WhenAll</c> -- porque la deteccion escribe
/// (<see cref="DetectAnomaliesUseCase"/>) y el <c>AppDbContext</c>
/// compartido no debe ver una escritura solaparse con una lectura.
/// </summary>
public sealed class GetDashboardInsightsQuery
{
    /// <summary>Cantidad maxima de categorias principales enviadas a la narracion de insights (D-10).</summary>
    public const int MaxTopCategories = 3;

    /// <summary>Cantidad maxima de lineas presupuesto-vs-real determinadas enviadas a la narracion de insights (D-10).</summary>
    public const int MaxTopBudgetLines = 3;

    private static readonly IReadOnlyDictionary<Guid, string> _emptyMessages = new Dictionary<Guid, string>();

    private readonly GetMonthEndForecastUseCase _forecast;
    private readonly DetectAnomaliesUseCase _detectAnomalies;
    private readonly AcknowledgeAnomalyUseCase _acknowledgeAnomaly;
    private readonly GetMonthSummaryQuery _monthSummary;
    private readonly GetCategoryBreakdownQuery _categoryBreakdown;
    private readonly GetBudgetVsActualQuery _budgetVsActual;
    private readonly GetInsightsUseCase _insights;
    private readonly GetAnomalyNarrativeUseCase _anomalyNarrative;
    private readonly ILogger<GetDashboardInsightsQuery> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="GetDashboardInsightsQuery"/>.
    /// </summary>
    public GetDashboardInsightsQuery(
        GetMonthEndForecastUseCase forecast,
        DetectAnomaliesUseCase detectAnomalies,
        AcknowledgeAnomalyUseCase acknowledgeAnomaly,
        GetMonthSummaryQuery monthSummary,
        GetCategoryBreakdownQuery categoryBreakdown,
        GetBudgetVsActualQuery budgetVsActual,
        GetInsightsUseCase insights,
        GetAnomalyNarrativeUseCase anomalyNarrative,
        ILogger<GetDashboardInsightsQuery> logger)
    {
        ArgumentNullException.ThrowIfNull(forecast);
        ArgumentNullException.ThrowIfNull(detectAnomalies);
        ArgumentNullException.ThrowIfNull(acknowledgeAnomaly);
        ArgumentNullException.ThrowIfNull(monthSummary);
        ArgumentNullException.ThrowIfNull(categoryBreakdown);
        ArgumentNullException.ThrowIfNull(budgetVsActual);
        ArgumentNullException.ThrowIfNull(insights);
        ArgumentNullException.ThrowIfNull(anomalyNarrative);
        ArgumentNullException.ThrowIfNull(logger);

        _forecast = forecast;
        _detectAnomalies = detectAnomalies;
        _acknowledgeAnomaly = acknowledgeAnomaly;
        _monthSummary = monthSummary;
        _categoryBreakdown = categoryBreakdown;
        _budgetVsActual = budgetVsActual;
        _insights = insights;
        _anomalyNarrative = anomalyNarrative;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el paso deterministico (pronostico, deteccion de
    /// anomalias, y las cifras de insights) para el mes que contiene
    /// <paramref name="monthAnchor"/>. Nunca resuelve un
    /// <c>ILLMClient</c> ni depende de <c>ISettingsStore</c> (S-9).
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha dentro del mes evaluado.</param>
    /// <param name="ct">Token de cancelacion opcional.</param>
    /// <returns>
    /// <see cref="Result.Success(object)"/> con un
    /// <see cref="DashboardInsightsSnapshot"/>. Nunca falla: un paso que
    /// lanza se registra (<c>LogError</c>) y su seccion queda
    /// <see langword="null"/> sin afectar a las demas.
    /// </returns>
    public async Task<Result> GetDeterministicAsync(DateOnly monthAnchor, CancellationToken ct = default)
    {
        DateOnly anchor = MonthPeriod.Anchor(monthAnchor);

        MonthEndForecastDto? forecast = await RunStepAsync<MonthEndForecastDto>(
            () => _forecast.ExecuteAsync(anchor, ct), "forecast", anchor, ct).ConfigureAwait(false);

        AnomalyDetectionResult? detection = await RunStepAsync<AnomalyDetectionResult>(
            () => _detectAnomalies.ExecuteAsync(anchor, ct), "anomaly detection", anchor, ct).ConfigureAwait(false);
        IReadOnlyList<AnomalyItemDto>? anomalies = detection?.Items;

        InsightFacts? facts = await BuildFactsAsync(anchor, forecast, anomalies, ct).ConfigureAwait(false);

        bool hasMissingRates =
            (forecast?.HasMissingRates ?? false) ||
            (detection?.HasMissingRates ?? false) ||
            (facts?.HasMissingRates ?? false);

        return Result.Success(new DashboardInsightsSnapshot(anchor, forecast, anomalies, facts, hasMissingRates));
    }

    /// <summary>
    /// Ejecuta el paso de narracion sobre un <paramref name="snapshot"/>
    /// ya calculado. Corre <see cref="GetInsightsUseCase"/> y
    /// <see cref="GetAnomalyNarrativeUseCase"/> concurrentemente
    /// (D-11) -- a lo sumo 2 llamadas HTTP por carga. Ambos use cases
    /// son success-shaped por si mismos; un <c>catch</c> defensivo cubre
    /// cualquier cosa inesperada (D-10).
    /// </summary>
    /// <param name="snapshot">Snapshot deterministico previamente calculado.</param>
    /// <param name="ct">Token de cancelacion opcional (superseder por navegacion).</param>
    public async Task<Result> GetNarrationAsync(DashboardInsightsSnapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Task<Result> insightsTask = snapshot.Facts is not null
            ? _insights.ExecuteAsync(snapshot.Facts, ct)
            : Task.FromResult(Result.Success(new InsightsNarrative(NarrativeStatus.Skipped, null)));

        IReadOnlyList<AnomalyItemDto> anomaliesForNarrative = snapshot.Anomalies ?? [];
        Task<Result> anomalyTask = _anomalyNarrative.ExecuteAsync(anomaliesForNarrative, ct);

        try
        {
            await Task.WhenAll(insightsTask, anomalyTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard narration step threw unexpectedly for {Month}.", snapshot.Month);
            return Result.Success(new DashboardNarration(
                new InsightsNarrative(NarrativeStatus.Unavailable, null),
                new AnomalyNarration(NarrativeStatus.Unavailable, _emptyMessages)));
        }

        InsightsNarrative insightsNarrative = insightsTask.Result.IsSuccess
            ? (InsightsNarrative)insightsTask.Result.Value!
            : new InsightsNarrative(NarrativeStatus.Unavailable, null);

        AnomalyNarration anomalyNarration = anomalyTask.Result.IsSuccess
            ? (AnomalyNarration)anomalyTask.Result.Value!
            : new AnomalyNarration(NarrativeStatus.Unavailable, _emptyMessages);

        return Result.Success(new DashboardNarration(insightsNarrative, anomalyNarration));
    }

    /// <summary>
    /// Descarta (reconoce) una anomalia por fila (R-9: la unica accion
    /// de descarte permitida en esta fase). Delega directamente en
    /// <see cref="AcknowledgeAnomalyUseCase"/>.
    /// </summary>
    public Task<Result> DismissAnomalyAsync(Guid alertId, CancellationToken ct = default) =>
        _acknowledgeAnomaly.ExecuteAsync(alertId, ct);

    /// <summary>
    /// Ejecuta las tres queries existentes que componen
    /// <see cref="InsightFacts"/> (resumen del mes, desglose por
    /// categoria, presupuesto vs. real) SECUENCIALMENTE, con un unico
    /// <c>try/catch</c> alrededor de las tres -- una falla en cualquiera
    /// de ellas deja <c>Facts</c> en <see langword="null"/> (la narracion
    /// de insights se salta), sin afectar al pronostico ni a las
    /// anomalias (D-10).
    /// </summary>
    private async Task<InsightFacts?> BuildFactsAsync(
        DateOnly anchor, MonthEndForecastDto? forecast, IReadOnlyList<AnomalyItemDto>? anomalies, CancellationToken ct)
    {
        try
        {
            Result summaryResult = await _monthSummary.ExecuteAsync(anchor, ct).ConfigureAwait(false);
            if (!summaryResult.IsSuccess)
            {
                LogFactsFailure(anchor, summaryResult);
                return null;
            }

            var summary = (MonthSummaryResult)summaryResult.Value!;

            Result breakdownResult = await _categoryBreakdown.ExecuteAsync(anchor, ct: ct).ConfigureAwait(false);
            if (!breakdownResult.IsSuccess)
            {
                LogFactsFailure(anchor, breakdownResult);
                return null;
            }

            var breakdown = (CategoryBreakdownResult)breakdownResult.Value!;

            Result budgetResult = await _budgetVsActual.ExecuteAsync(anchor, ct).ConfigureAwait(false);
            if (!budgetResult.IsSuccess)
            {
                LogFactsFailure(anchor, budgetResult);
                return null;
            }

            var budgetVsActual = (BudgetVsActualResult)budgetResult.Value!;

            var topCategories = breakdown.Slices
                .Where(s => s.CategoryId is not null)
                .Take(MaxTopCategories)
                .ToList();

            var topBudgetLines = budgetVsActual.Lines
                .Where(l => !l.IsIndeterminate)
                .Take(MaxTopBudgetLines)
                .ToList();

            bool hasMissingRates = summary.HasMissingRates || breakdown.HasMissingRates || budgetVsActual.HasMissingRates;

            return new InsightFacts(
                anchor,
                summary.ExpensesDop,
                summary.IncomesDop,
                summary.NetBalanceDop,
                summary.ExpensesDeltaPercent,
                topCategories,
                topBudgetLines,
                forecast?.ProjectedDop,
                forecast?.BudgetTotalDop,
                anomalies?.Count ?? 0,
                hasMissingRates);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard facts step threw for {Month}.", anchor);
            return null;
        }
    }

    private void LogFactsFailure(DateOnly anchor, Result result) =>
        _logger.LogError(
            "Dashboard facts step failed for {Month}: {ErrorCode} {ErrorMessage}",
            anchor,
            result.Error?.Code,
            result.Error?.Message);

    /// <summary>
    /// Ejecuta un paso deterministico dentro de su propio
    /// <c>try/catch</c> (D-10): una falla de <see cref="Result"/> o una
    /// excepcion se registran (<c>LogError</c>) y devuelven
    /// <see langword="null"/> sin propagar -- un bug de pronostico no
    /// puede vaciar la lista de anomalias, y viceversa.
    /// </summary>
    private async Task<T?> RunStepAsync<T>(
        Func<Task<Result>> step, string stepName, DateOnly anchor, CancellationToken ct)
        where T : class
    {
        try
        {
            Result result = await step().ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                _logger.LogError(
                    "Dashboard {Step} step failed for {Month}: {ErrorCode} {ErrorMessage}",
                    stepName,
                    anchor,
                    result.Error?.Code,
                    result.Error?.Message);
                return null;
            }

            return (T?)result.Value;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard {Step} step threw for {Month}.", stepName, anchor);
            return null;
        }
    }
}
