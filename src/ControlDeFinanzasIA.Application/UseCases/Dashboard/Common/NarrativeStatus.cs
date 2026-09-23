namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.Common;

/// <summary>
/// Resultado de un paso de narración por IA del dashboard (F-014, slice 5/6,
/// D-8). Ambos casos de uso de narración (<c>GetInsightsUseCase</c>,
/// <c>GetAnomalyNarrativeUseCase</c>) son "success-shaped": ninguna variante
/// de este enum corresponde a un <c>Result.Failure</c> — la narración
/// siempre devuelve <c>Result.Success</c> con el estado adecuado (R-4, R-6).
/// </summary>
public enum NarrativeStatus
{
    /// <summary>El modelo generó texto usable; el resultado puede cachearse (D-11).</summary>
    Generated = 0,

    /// <summary>No hay proveedor/clave de IA configurados. Cero llamadas HTTP.</summary>
    NotConfigured = 1,

    /// <summary>La llamada al modelo falló, expiró, o la respuesta no fue parseable.</summary>
    Unavailable = 2,

    /// <summary>No había nada que narrar (p. ej. cero anomalías, o cero gastos e ingresos).</summary>
    Skipped = 3,
}
