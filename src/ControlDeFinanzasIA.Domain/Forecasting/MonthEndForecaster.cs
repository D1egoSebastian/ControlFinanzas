namespace ControlDeFinanzasIA.Domain.Forecasting;

/// <summary>
/// Estado explicito de una proyeccion de fin de mes (F-014, R-1,
/// Reconciliation punto 7 — superset del spec: agrega
/// <see cref="ClosedMonth"/> y <see cref="FutureMonth"/>).
/// </summary>
public enum ForecastState
{
    /// <summary>Mes actual con proyeccion numerica valida.</summary>
    Projected = 0,

    /// <summary>Mes actual, sin historial y con menos de <see cref="MonthEndForecaster.MinElapsedDaysWithoutHistory"/> dias transcurridos.</summary>
    InsufficientData = 1,

    /// <summary>Mes ya finalizado: "pasado = real", sin proyeccion (D10).</summary>
    ClosedMonth = 2,

    /// <summary>Mes futuro: aun no ha comenzado.</summary>
    FutureMonth = 3
}

/// <summary>
/// Resultado de <see cref="MonthEndForecaster.Project"/>.
/// </summary>
/// <param name="ProjectedDop">
/// Proyeccion de fin de mes, redondeada. <see langword="null"/> para
/// <see cref="ForecastState.InsufficientData"/> y
/// <see cref="ForecastState.FutureMonth"/>. Para
/// <see cref="ForecastState.ClosedMonth"/> es igual a
/// <paramref name="SpentToDateDop"/> (D10 "pasado = real").
/// </param>
/// <param name="DailyRateDop">
/// Tasa diaria combinada, redondeada, solo para display/tests.
/// <see langword="null"/> salvo en <see cref="ForecastState.Projected"/>.
/// </param>
public readonly record struct MonthEndForecast(
    ForecastState State,
    decimal SpentToDateDop,
    decimal? ProjectedDop,
    decimal? DailyRateDop,
    int ElapsedDays,
    int DaysInMonth);

/// <summary>
/// Servicio de dominio puro (F-014, R-1) que proyecta el gasto de fin de
/// mes combinando el ritmo del mes en curso con el promedio diario
/// historico. No realiza I/O, no resuelve dependencias via DI y no
/// realiza conversion de moneda: recibe montos ya normalizados a DOP
/// por el caller (D7), exactamente como <c>BudgetEvaluator</c>.
/// </summary>
/// <remarks>
/// Clase estatica, mismo patron que <c>BudgetEvaluator</c> /
/// <c>SavingsPlanCalculator</c>. La comparacion contra el presupuesto
/// NO vive aqui: el caller reutiliza <c>BudgetEvaluator.Evaluate</c>
/// (D-2) para tener una unica fuente de verdad de "cerca/excedido".
/// </remarks>
public static class MonthEndForecaster
{
    /// <summary>
    /// Dias minimos transcurridos, sin historial, antes de que una
    /// proyeccion pura por ritmo se considere confiable.
    /// </summary>
    public const int MinElapsedDaysWithoutHistory = 3;

    /// <summary>
    /// Meses completos previos que el caller promedia para construir
    /// <c>historicalDailyAverageDop</c>. No usada internamente; documenta
    /// el contrato con el caller (mismo patron que
    /// <c>AnomalyDetector.HistoryWindowMonths</c>).
    /// </summary>
    public const int HistoryMonths = 3;

    /// <summary>Decimales usados al redondear el monto proyectado y la tasa diaria.</summary>
    public const int AmountDecimals = 2;

    /// <summary>
    /// Proyecta el gasto de fin de mes. <c>projected = spentToDate +
    /// remainingDays × dailyRate</c>, donde <c>dailyRate = w·runRate +
    /// (1−w)·historicalDailyAverageDop</c> y <c>w =
    /// elapsedDays/daysInMonth</c>. Sin historial, cae a puro ritmo
    /// (<c>runRate</c>).
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha del mes evaluado (tipicamente su primer dia).</param>
    /// <param name="today">Fecha actual, inyectada por el caller para determinismo en tests.</param>
    /// <param name="spentToDateDop">Gasto acumulado a la fecha, ya normalizado a DOP. Debe ser &gt;= 0.</param>
    /// <param name="historicalDailyAverageDop">
    /// Promedio diario de los <see cref="HistoryMonths"/> meses completos
    /// previos, ya normalizado a DOP. <see langword="null"/> significa
    /// "sin historial". Cuando no es <see langword="null"/>, debe ser
    /// &gt;= 0.
    /// </param>
    /// <returns>Un <see cref="MonthEndForecast"/> con el estado explicito y la proyeccion, cuando aplica.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se lanza si <paramref name="spentToDateDop"/> o
    /// <paramref name="historicalDailyAverageDop"/> (cuando no es
    /// <see langword="null"/>) son negativos.
    /// </exception>
    public static MonthEndForecast Project(
        DateOnly monthAnchor, DateOnly today, decimal spentToDateDop, decimal? historicalDailyAverageDop)
    {
        if (spentToDateDop < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(spentToDateDop), spentToDateDop, "spentToDateDop must be non-negative.");
        }

        if (historicalDailyAverageDop is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(historicalDailyAverageDop), historicalDailyAverageDop,
                "historicalDailyAverageDop must be non-negative when not null.");
        }

        int daysInMonth = DateTime.DaysInMonth(monthAnchor.Year, monthAnchor.Month);

        // Compared by (year, month), not by raw DateOnly, so the result never depends on
        // which day of the anchor month the caller happens to pass.
        (int Year, int Month) anchorMonth = (monthAnchor.Year, monthAnchor.Month);
        (int Year, int Month) todayMonth = (today.Year, today.Month);

        if (todayMonth.CompareTo(anchorMonth) < 0)
        {
            return new MonthEndForecast(ForecastState.FutureMonth, spentToDateDop, null, null, 0, daysInMonth);
        }

        if (todayMonth.CompareTo(anchorMonth) > 0)
        {
            // Past = actual (D10): the projection IS the accumulated spend, no rate involved.
            return new MonthEndForecast(
                ForecastState.ClosedMonth, spentToDateDop, spentToDateDop, null, daysInMonth, daysInMonth);
        }

        int elapsedDays = today.Day;
        int remainingDays = daysInMonth - elapsedDays;
        bool hasHistory = historicalDailyAverageDop is not null;

        if (!hasHistory && elapsedDays < MinElapsedDaysWithoutHistory)
        {
            return new MonthEndForecast(
                ForecastState.InsufficientData, spentToDateDop, null, null, elapsedDays, daysInMonth);
        }

        decimal runRate = spentToDateDop / elapsedDays;
        decimal weight = (decimal)elapsedDays / daysInMonth;
        decimal rate = hasHistory
            ? (weight * runRate) + ((1 - weight) * historicalDailyAverageDop!.Value)
            : runRate;

        decimal projected = Math.Round(
            spentToDateDop + (remainingDays * rate), AmountDecimals, MidpointRounding.AwayFromZero);
        decimal roundedRate = Math.Round(rate, AmountDecimals, MidpointRounding.AwayFromZero);

        return new MonthEndForecast(
            ForecastState.Projected, spentToDateDop, projected, roundedRate, elapsedDays, daysInMonth);
    }
}
