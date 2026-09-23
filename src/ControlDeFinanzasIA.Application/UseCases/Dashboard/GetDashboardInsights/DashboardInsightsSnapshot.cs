using ControlDeFinanzasIA.Application.UseCases.Dashboard.DetectAnomalies;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetAnomalyNarrative;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetInsights;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthEndForecast;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetDashboardInsights;

/// <summary>
/// Resultado del paso deterministico de <see cref="GetDashboardInsightsQuery.GetDeterministicAsync"/>
/// (F-014, R-7, D-10). Cada seccion se calcula de forma independiente
/// -- un error en una NO vacia las demas -- por eso cada campo es
/// nullable con su propio significado de "el paso fallo".
/// </summary>
/// <param name="Month">Primer dia del mes calendario evaluado.</param>
/// <param name="Forecast">
/// Proyeccion de fin de mes, o <see langword="null"/> si el paso de
/// pronostico lanzo una excepcion (registrada como Error).
/// </param>
/// <param name="Anomalies">
/// Anomalias activas del mes, en orden del detector, o
/// <see langword="null"/> si el paso de deteccion lanzo una excepcion.
/// </param>
/// <param name="Facts">
/// Cifras ya calculadas para narrar insights, o <see langword="null"/>
/// si alguna de las tres queries que las componen fallo -- en ese caso
/// la narracion de insights se salta (<c>NarrativeStatus.Skipped</c>).
/// </param>
/// <param name="HasMissingRates">
/// <see langword="true"/> si alguna conversion de moneda fallo en
/// cualquiera de los pasos deterministicos que se completaron.
/// </param>
public sealed record DashboardInsightsSnapshot(
    DateOnly Month,
    MonthEndForecastDto? Forecast,
    IReadOnlyList<AnomalyItemDto>? Anomalies,
    InsightFacts? Facts,
    bool HasMissingRates);

/// <summary>
/// Resultado del paso de narracion de <see cref="GetDashboardInsightsQuery.GetNarrationAsync"/>
/// (F-014, R-7, D-10): la combinacion de ambos resultados success-shaped
/// de <see cref="GetInsightsUseCase"/> y <see cref="GetAnomalyNarrativeUseCase"/>,
/// emitidos por UNA sola llamada a <c>Task.WhenAll</c> (D-11: a lo sumo 2
/// llamadas HTTP concurrentes por carga).
/// </summary>
/// <param name="Insights">Narracion de insights del mes.</param>
/// <param name="Anomalies">Narracion por lotes de las anomalias con mayor puntaje.</param>
public sealed record DashboardNarration(InsightsNarrative Insights, AnomalyNarration Anomalies);
