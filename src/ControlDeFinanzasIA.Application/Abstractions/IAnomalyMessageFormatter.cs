using ControlDeFinanzasIA.Domain.Alerts;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Hechos deterministicos necesarios para construir el texto plantilla
/// (<see cref="Domain.Alerts.AnomalyAlert.Message"/>) de una anomalia
/// detectada (F-014, D-7). Puramente informativo: no contiene ningun
/// texto ya formateado.
/// </summary>
/// <param name="CategoryName">Nombre de la categoria del gasto marcado.</param>
/// <param name="AmountDop">Monto del gasto candidato, normalizado a DOP.</param>
/// <param name="MedianDop">Mediana historica de la categoria, normalizada a DOP.</param>
/// <param name="RatioToMedian"><c>round(AmountDop / MedianDop, 2)</c>, calculada por <see cref="AnomalyDetector"/>.</param>
/// <param name="Severity">Severidad de la anomalia.</param>
public sealed record AnomalyMessageFacts(
    string CategoryName,
    decimal AmountDop,
    decimal MedianDop,
    decimal RatioToMedian,
    AnomalySeverity Severity);

/// <summary>
/// Puerto de Application (F-014, R-8 vs D-9) hacia el formateador de texto
/// plantilla deterministico de <see cref="Domain.Alerts.AnomalyAlert.Message"/>.
/// Implementado en UI sobre <c>Strings.resx</c>
/// (<c>UI/Formatting/ResxAnomalyMessageFormatter</c>, slice S7) para que
/// Application nunca contenga literales de cara al usuario, mientras el
/// texto persistido sigue siendo puramente deterministico (nunca proviene
/// de un LLM).
/// </summary>
public interface IAnomalyMessageFormatter
{
    /// <summary>
    /// Formatea el texto plantilla deterministico y localizado a partir de
    /// <paramref name="facts"/>. Puro: nunca invoca un LLM ni realiza I/O.
    /// </summary>
    /// <param name="facts">Hechos deterministicos de la anomalia.</param>
    /// <returns>Texto plantilla listo para <see cref="Domain.Alerts.AnomalyAlert.Create"/>.</returns>
    string Format(AnomalyMessageFacts facts);
}
