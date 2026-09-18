namespace ControlDeFinanzasIA.Application.UseCases.Calendar.GetCalendarMonth;

/// <summary>
/// Un día visible en la grilla del calendario (D-3). <see cref="TotalDop"/>
/// es nullable para distinguir "cero gastos" (día del mes navegado,
/// <c>0m</c>) de "no aplica" (día de un mes adyacente, relleno,
/// <see langword="null"/>) — spec R-2/R-3.
/// </summary>
/// <param name="Date">Fecha de la celda.</param>
/// <param name="TotalDop">
/// Total del día normalizado a DOP; <c>0m</c> si <see cref="Date"/>
/// pertenece al mes navegado y no tiene gastos, <see langword="null"/> si
/// es un día de un mes adyacente (relleno, sin badge).
/// </param>
/// <param name="IsCurrentMonth">
/// <see langword="true"/> si <see cref="Date"/> pertenece al mes navegado
/// (<see cref="CalendarMonthResult.MonthAnchor"/>), <see langword="false"/>
/// para días de relleno de meses adyacentes.
/// </param>
/// <param name="IsToday">
/// <see langword="true"/> si <see cref="Date"/> es la fecha actual
/// (<see cref="Abstractions.IClock.Today"/>).
/// </param>
public sealed record CalendarDay(DateOnly Date, decimal? TotalDop, bool IsCurrentMonth, bool IsToday);

/// <summary>
/// Resultado de la grilla mensual del calendario (R-1, R-2, R-3, D-3):
/// siempre <see cref="Common.CalendarGrid.CellCount"/> (42) días.
/// </summary>
/// <param name="MonthAnchor">Primer día del mes navegado.</param>
/// <param name="Days">Los 42 días de la grilla, en orden.</param>
/// <param name="MonthTotalDop">
/// Suma de los totales del mes navegado (excluye días de relleno de meses
/// adyacentes), ya normalizada a DOP.
/// </param>
/// <param name="HasMissingRates">
/// <see langword="true"/> si al menos una conversión a DOP falló (R-2); ese
/// bucket se excluye de la suma de su día, sin lanzar excepción.
/// </param>
public sealed record CalendarMonthResult(
    DateOnly MonthAnchor,
    IReadOnlyList<CalendarDay> Days,
    decimal MonthTotalDop,
    bool HasMissingRates);
