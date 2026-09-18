using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.DTOs.Aggregates;

/// <summary>
/// Total agregado por categoría x moneda dentro de un periodo. Grano:
/// una fila por combinación de categoría y moneda (D-1).
/// <see cref="CategoryName"/> proviene de un join SQL a <c>Categories</c>
/// en <c>GetCategoryTotalsAsync</c>. Devuelve TODAS las categorías del
/// periodo; el ranking top-N + "Otras" se calcula en la capa Application (D-2).
/// </summary>
/// <param name="CategoryId">Identificador de la categoría.</param>
/// <param name="CategoryName">Etiqueta de la categoría (join a <c>Categories</c>).</param>
/// <param name="Currency">Moneda de este bucket.</param>
/// <param name="Amount">Suma sin convertir para esa categoría y moneda.</param>
public sealed record CategoryCurrencyTotal(
    Guid CategoryId, string CategoryName, Currency Currency, decimal Amount);
