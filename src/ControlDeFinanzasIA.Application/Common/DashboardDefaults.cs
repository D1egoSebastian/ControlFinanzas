namespace ControlDeFinanzasIA.Application.Common;

/// <summary>
/// Constantes con nombre que parametrizan el Dashboard (Fase 8), para no
/// dispersar números mágicos entre las queries y la UI. Ver
/// <c>spec/features/phase-08/F-008-dashboard-visualizations.spec.md</c>
/// (R-4, R-5, R-6) y el design.md de <c>f-008p8-dashboard-visualizations</c>
/// (D-4, D-5) para el razonamiento detrás de cada valor.
/// </summary>
public static class DashboardDefaults
{
    /// <summary>
    /// Número de categorías individuales mostradas en el gráfico de
    /// categorías antes de agrupar el resto en "Otras" (R-4).
    /// </summary>
    public const int TopCategories = 5;

    /// <summary>
    /// Tamaño de la ventana, en meses, del gráfico de evolución temporal
    /// (R-5, D-4). 6 en vez de 12 porque los datos reales empiezan en
    /// Fase 4; ampliar a 12 es un cambio de una línea.
    /// </summary>
    public const int EvolutionMonths = 6;

    /// <summary>
    /// Horizonte, en días desde <c>IClock.Today</c>, dentro del cual un
    /// pago se considera "próximo" (R-6, D-5). 30 en vez de 14: con 14 días
    /// una obligación mensual es visible solo ~47% de los días de su ciclo.
    /// </summary>
    public const int UpcomingHorizonDays = 30;

    /// <summary>
    /// Máximo de elementos mostrados en la tarjeta "Próximos pagos" (R-6).
    /// </summary>
    public const int UpcomingMaxItems = 10;
}
