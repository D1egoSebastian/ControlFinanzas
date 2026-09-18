using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.DTOs.Aggregates;

/// <summary>
/// Total agregado por moneda dentro de un periodo. Grano: una fila por
/// cada moneda presente en el periodo (D-1). Producido por
/// <c>GetPeriodTotalsAsync</c> de <see cref="Abstractions.IExpenseRepository"/>
/// y <see cref="Abstractions.IIncomeRepository"/>.
/// </summary>
/// <param name="Currency">Moneda de este bucket.</param>
/// <param name="Amount">Suma sin convertir (columna <c>Amount.Amount</c>) para esa moneda.</param>
public sealed record CurrencyTotal(Currency Currency, decimal Amount);
