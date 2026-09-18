namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;

/// <summary>
/// Una porción del gráfico de categorías: una categoría individual, o
/// la porción sintética "Otras" que agrupa el resto (R-4).
/// </summary>
/// <param name="CategoryId">
/// Identificador de la categoría, o <see langword="null"/> para la
/// porción "Otras".
/// </param>
/// <param name="Label">Etiqueta a mostrar (nombre de categoría, u "Otras").</param>
/// <param name="AmountDop">Monto de esta porción, ya normalizado a DOP.</param>
public sealed record CategorySlice(Guid? CategoryId, string Label, decimal AmountDop);

/// <summary>
/// Resultado del gráfico de categorías del mes seleccionado (R-4).
/// </summary>
/// <param name="Slices">
/// Hasta <c>topCount</c> categorías individuales ordenadas
/// descendentemente por <see cref="CategorySlice.AmountDop"/>, seguidas
/// por la porción "Otras" (<see cref="CategorySlice.CategoryId"/> =
/// <see langword="null"/>) cuando hay más de <c>topCount</c> categorías
/// con gasto en el periodo (D-2).
/// </param>
/// <param name="TotalDop">
/// Suma de <em>todas</em> las categorías del periodo (no solo las
/// mostradas), ya normalizada a DOP. La suma de <see cref="Slices"/>
/// reconcilia con este total (spec R-4).
/// </param>
/// <param name="HasMissingRates">
/// <see langword="true"/> si al menos un bucket por moneda no pudo
/// convertirse a DOP (R-7, S-6); ese bucket se excluye de la suma de
/// su categoría, sin lanzar excepción.
/// </param>
public sealed record CategoryBreakdownResult(
    IReadOnlyList<CategorySlice> Slices,
    decimal TotalDop,
    bool HasMissingRates);
