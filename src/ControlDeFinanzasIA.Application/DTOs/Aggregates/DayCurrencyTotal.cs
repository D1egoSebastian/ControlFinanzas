using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.DTOs.Aggregates;

/// <summary>
/// Total agregado por día x moneda dentro de un periodo. Grano: una fila
/// por combinación de fecha y moneda (D-1), sibling de
/// <see cref="MonthCurrencyTotal"/>. Producido por
/// <c>GetDailyTotalsAsync</c> de <see cref="Abstractions.IExpenseRepository"/>
/// y consumido por la vista de calendario (F-009).
/// </summary>
/// <param name="Date">Fecha calendario del bucket.</param>
/// <param name="Currency">Moneda de este bucket.</param>
/// <param name="Amount">Suma sin convertir para esa fecha y moneda.</param>
public sealed record DayCurrencyTotal(DateOnly Date, Currency Currency, decimal Amount);
