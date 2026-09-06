namespace ControlDeFinanzasIA.Domain.ValueObjects;

/// <summary>
/// Value object inmutable que representa un rango de fechas
/// <b>inclusivo en ambos extremos</b>. Dos <see cref="DateRange"/>
/// son iguales si tienen el mismo <see cref="Start"/> y el mismo
/// <see cref="End"/>.
///
/// No se puede construir directamente: el único camino es a través
/// de <see cref="Create"/>, que valida que
/// <see cref="Start"/> &lt;= <see cref="End"/>. Esta garantía
/// preserva la invariante del tipo y desbloquea las operaciones
/// de <see cref="Contains"/>, <see cref="Overlaps"/>,
/// <see cref="Intersect"/>, y <see cref="DurationInDays"/>.
/// </summary>
public sealed record DateRange
{
    /// <summary>
    /// Inicio del rango (inclusivo). Validado en <see cref="Create"/>.
    /// Invariante: <c>Start &lt;= End</c>.
    /// </summary>
    public DateOnly Start { get; }

    /// <summary>
    /// Fin del rango (inclusivo). Validado en <see cref="Create"/>.
    /// Invariante: <c>Start &lt;= End</c>.
    /// </summary>
    public DateOnly End { get; }

    private DateRange(DateOnly start, DateOnly end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Crea un <see cref="DateRange"/> validando que
    /// <paramref name="start"/> sea menor o igual que
    /// <paramref name="end"/>. Es el único camino válido para
    /// construir una instancia.
    /// </summary>
    /// <param name="start">Inicio del rango (inclusivo).</param>
    /// <param name="end">Fin del rango (inclusivo). Debe ser &gt;= <paramref name="start"/>.</param>
    /// <returns>Una instancia válida de <see cref="DateRange"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Se lanza si <paramref name="start"/> es estrictamente mayor
    /// que <paramref name="end"/>. El mensaje de la excepción
    /// incluye los valores recibidos para facilitar el diagnóstico.
    /// </exception>
    /// <remarks>
    /// La validación se aplica <b>una sola vez</b> (en el factory).
    /// Las operaciones derivadas (<see cref="Contains"/>,
    /// <see cref="Overlaps"/>, etc.) confían en la invariante y
    /// no revalidan.
    /// </remarks>
    public static DateRange Create(DateOnly start, DateOnly end)
    {
        if (start > end)
        {
            throw new ArgumentException(
                $"Start must be less than or equal to End. Received: start={start:o}, end={end:o}.",
                nameof(start));
        }
        return new DateRange(start, end);
    }

    public bool Contains(DateOnly date){
        if( date >= Start && date <= End){
            return true;
        }
        return false;
    }

    public bool Overlaps(DateRange other){
        if(other == null){
            throw new ArgumentNullException(nameof(other));
        }

        return this.Start <= other.End && other.Start <= this.End;
    }

    public int DurationInDays() => End.DayNumber - Start.DayNumber + 1;

    public DateRange? Intersect(DateRange other){
        if(other == null){
            throw new ArgumentNullException(nameof(other));
        }

        if(Start > other.End || other.Start > End){
            return null;
        }

        return Create(
            Start > other.Start ? Start : other.Start,
            End < other.End ? End : other.End);
    }
}
