using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.Common;

/// <summary>
/// Funciones puras de cálculo de periodo mensual usadas por el Dashboard
/// (Fase 8). <see cref="DateRange"/> sigue siendo el contrato del
/// repositorio (inclusivo en ambos extremos); la semántica de "mes" vive
/// aquí para mantener la navegación de periodo testeable fuera del
/// ViewModel.
/// </summary>
public static class MonthPeriod
{
    /// <summary>
    /// Normaliza cualquier fecha al primer día de su mes calendario.
    /// </summary>
    /// <param name="any">Una fecha cualquiera dentro del mes.</param>
    /// <returns>El primer día del mes que contiene <paramref name="any"/>.</returns>
    public static DateOnly Anchor(DateOnly any) => new(any.Year, any.Month, 1);

    /// <summary>
    /// Calcula el <see cref="DateRange"/> inclusivo del mes calendario que
    /// contiene <paramref name="monthAnchor"/>: desde el primer día hasta
    /// el último día de ese mes.
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha dentro del mes deseado.</param>
    public static DateRange Range(DateOnly monthAnchor)
    {
        DateOnly start = Anchor(monthAnchor);
        DateOnly end = start.AddMonths(1).AddDays(-1);
        return DateRange.Create(start, end);
    }

    /// <summary>
    /// Devuelve el primer día del mes calendario anterior al que contiene
    /// <paramref name="monthAnchor"/>. Diciembre → enero del año anterior.
    /// </summary>
    public static DateOnly Previous(DateOnly monthAnchor) => Anchor(monthAnchor).AddMonths(-1);

    /// <summary>
    /// Devuelve el primer día del mes calendario siguiente al que contiene
    /// <paramref name="monthAnchor"/>. Diciembre → enero del año siguiente.
    /// </summary>
    public static DateOnly Next(DateOnly monthAnchor) => Anchor(monthAnchor).AddMonths(1);

    /// <summary>
    /// Calcula el <see cref="DateRange"/> inclusivo de una ventana de
    /// <paramref name="months"/> meses consecutivos que termina en (e
    /// incluye) el mes de <paramref name="monthAnchor"/>. Usado por el
    /// gráfico de evolución temporal (R-5) con
    /// <see cref="DashboardDefaults.EvolutionMonths"/>.
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha dentro del último mes de la ventana.</param>
    /// <param name="months">Cantidad de meses consecutivos en la ventana. Debe ser mayor que cero.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="months"/> no es positivo.</exception>
    public static DateRange TrailingRange(DateOnly monthAnchor, int months)
    {
        if (months <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(months), months, "months must be greater than zero.");
        }

        DateOnly lastMonthAnchor = Anchor(monthAnchor);
        DateOnly start = lastMonthAnchor.AddMonths(-(months - 1));
        DateOnly end = lastMonthAnchor.AddMonths(1).AddDays(-1);
        return DateRange.Create(start, end);
    }
}
