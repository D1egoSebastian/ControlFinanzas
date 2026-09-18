namespace ControlDeFinanzasIA.Application.Common;

/// <summary>
/// Funciones puras de cálculo de la grilla del calendario mensual (Fase 9,
/// F-009), sibling de <see cref="MonthPeriod"/>. Sin dependencias ni
/// referencia a cultura/UI (D-2): el primer día de la semana se recibe como
/// parámetro, no se lee de <see cref="System.Globalization.CultureInfo"/>
/// aquí, para mantener esta clase pura y testeable de forma determinística.
/// </summary>
public static class CalendarGrid
{
    /// <summary>
    /// Cantidad fija de celdas de la grilla: 6 semanas x 7 días. Fija (no
    /// dinámica según el mes) para mantener una altura de grilla estable
    /// (D-2) — el máximo real es 6 semanas leading + 31 días = 37, por lo
    /// que 42 siempre alcanza.
    /// </summary>
    public const int CellCount = 42;

    /// <summary>
    /// Cantidad de días de relleno del mes anterior necesarios para que la
    /// primera semana de la grilla empiece en <paramref name="firstDayOfWeek"/>.
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha dentro del mes navegado.</param>
    /// <param name="firstDayOfWeek">Primer día de la semana de la grilla.</param>
    /// <returns>Un valor entre 0 (el mes arranca justo en <paramref name="firstDayOfWeek"/>) y 6.</returns>
    public static int LeadingOffset(DateOnly monthAnchor, DayOfWeek firstDayOfWeek)
    {
        DateOnly anchor = MonthPeriod.Anchor(monthAnchor);
        return ((int)anchor.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
    }

    /// <summary>
    /// Primer día visible de la grilla: el primer día del mes navegado menos
    /// <see cref="LeadingOffset"/> días.
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha dentro del mes navegado.</param>
    /// <param name="firstDayOfWeek">Primer día de la semana de la grilla.</param>
    public static DateOnly GridStart(DateOnly monthAnchor, DayOfWeek firstDayOfWeek)
    {
        DateOnly anchor = MonthPeriod.Anchor(monthAnchor);
        return anchor.AddDays(-LeadingOffset(monthAnchor, firstDayOfWeek));
    }

    /// <summary>
    /// Devuelve exactamente <see cref="CellCount"/> fechas contiguas,
    /// empezando en <see cref="GridStart"/>, que cubren la grilla completa
    /// del mes navegado incluyendo el relleno de días adyacentes.
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha dentro del mes navegado.</param>
    /// <param name="firstDayOfWeek">Primer día de la semana de la grilla.</param>
    public static IReadOnlyList<DateOnly> Dates(DateOnly monthAnchor, DayOfWeek firstDayOfWeek)
    {
        DateOnly start = GridStart(monthAnchor, firstDayOfWeek);
        var dates = new List<DateOnly>(CellCount);
        for (int i = 0; i < CellCount; i++)
        {
            dates.Add(start.AddDays(i));
        }

        return dates;
    }
}
