using ControlDeFinanzasIA.Domain.Alerts;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.DetectAnomalies;

/// <summary>
/// Anomalia activa (no reconocida) para el mes consultado, lista para
/// mostrarse en el dashboard (F-014, D-7). Las cifras (<see cref="AmountDop"/>,
/// <see cref="MedianDop"/>, <see cref="RatioToMedian"/>) se recalculan en
/// cada carga a partir de <c>AnomalyDetector.Detect</c> — no se persisten en
/// <see cref="Domain.Alerts.AnomalyAlert"/>.
/// </summary>
/// <param name="AlertId">Identificador estable de la alerta persistida.</param>
/// <param name="ExpenseId">Identificador del gasto marcado.</param>
/// <param name="Date">Fecha del gasto al momento de esta deteccion.</param>
/// <param name="CategoryName">Nombre de la categoria del gasto marcado.</param>
/// <param name="Description">Descripcion corta del gasto marcado.</param>
/// <param name="AmountDop">Monto del gasto, normalizado a DOP.</param>
/// <param name="MedianDop">Mediana historica de la categoria, normalizada a DOP.</param>
/// <param name="RatioToMedian"><c>round(AmountDop / MedianDop, 2)</c>.</param>
/// <param name="Severity">Severidad de la anomalia.</param>
/// <param name="Message">Texto plantilla deterministico persistido.</param>
public sealed record AnomalyItemDto(
    Guid AlertId,
    Guid ExpenseId,
    DateOnly Date,
    string CategoryName,
    string Description,
    decimal AmountDop,
    decimal MedianDop,
    decimal RatioToMedian,
    AnomalySeverity Severity,
    string Message);

/// <summary>
/// Resultado de <see cref="DetectAnomaliesUseCase.ExecuteAsync"/> (F-014, D-7).
/// </summary>
/// <param name="Items">
/// Anomalias no reconocidas del mes evaluado, en el orden del detector
/// (<c>Severity</c> desc, luego <c>RatioToMedian</c> desc, luego <c>Date</c>
/// desc, luego <c>ExpenseId</c> asc).
/// </param>
/// <param name="HasMissingRates">
/// <see langword="true"/> si alguna conversion de moneda (historial o
/// candidato) fallo durante esta carga.
/// </param>
public sealed record AnomalyDetectionResult(IReadOnlyList<AnomalyItemDto> Items, bool HasMissingRates);
