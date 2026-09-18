using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.DTOs.Aggregates;

/// <summary>
/// Total agregado por mes x moneda dentro de un periodo. Grano: una fila
/// por combinación de año, mes y moneda (D-1). <see cref="Year"/> y
/// <see cref="Month"/> derivan de <c>Date.Year</c>/<c>Date.Month</c>, no de
/// un <see cref="DateOnly"/>, para evitar ruido del día dentro del mes.
/// </summary>
/// <param name="Year">Año calendario del bucket.</param>
/// <param name="Month">Mes calendario del bucket (1-12).</param>
/// <param name="Currency">Moneda de este bucket.</param>
/// <param name="Amount">Suma sin convertir para ese mes y moneda.</param>
public sealed record MonthCurrencyTotal(
    int Year, int Month, Currency Currency, decimal Amount);
