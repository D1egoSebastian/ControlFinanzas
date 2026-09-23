namespace ControlDeFinanzasIA.Domain.Alerts;

/// <summary>
/// Severidad de una anomalía detectada por <see cref="AnomalyDetector"/>
/// (F-014, R-2), derivada del z-score robusto ya redondeado.
/// </summary>
public enum AnomalySeverity
{
    /// <summary>Score robusto redondeado en <c>[3.5, 5)</c>.</summary>
    Info = 0,

    /// <summary>
    /// Score robusto redondeado en <c>[5, 8)</c>, o la regla de razón
    /// (<c>MAD = 0</c>) cuando <c>x &gt;= 3 · median</c>.
    /// </summary>
    Warning = 1,

    /// <summary>Score robusto redondeado en <c>[8, ∞)</c>.</summary>
    Critical = 2
}
