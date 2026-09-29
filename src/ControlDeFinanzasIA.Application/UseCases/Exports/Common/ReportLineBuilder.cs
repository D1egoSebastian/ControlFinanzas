using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.UseCases.Exports.Common;

/// <summary>
/// Maps expenses and incomes to <see cref="ReportTransactionLine"/> (F-018, D-3). Amounts are
/// normalized with <see cref="ICurrencyConverter.ConvertAsync"/> to DOP; a missing rate yields
/// a <c>null</c> DOP amount, never 0.
/// </summary>
internal sealed class ReportLineBuilder
{
    private readonly ICurrencyConverter _converter;
    private readonly IReadOnlyDictionary<Guid, string> _categoryNames;

    internal ReportLineBuilder(ICurrencyConverter converter, IReadOnlyDictionary<Guid, string> categoryNames)
    {
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(categoryNames);
        _converter = converter;
        _categoryNames = categoryNames;
    }

    internal async Task<ReportTransactionLine> BuildAsync(Expense expense, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(expense);
        return new ReportTransactionLine(
            expense.Date,
            ReportTransactionKind.Expense,
            expense.Description,
            NameOf(expense.CategoryId),
            expense.Amount.Amount,
            expense.Amount.Currency,
            await ToDopAsync(expense.Amount, ct).ConfigureAwait(false));
    }

    internal async Task<ReportTransactionLine> BuildAsync(Income income, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(income);
        return new ReportTransactionLine(
            income.Date,
            ReportTransactionKind.Income,
            income.Description,
            NameOf(income.CategoryId),
            income.Amount.Amount,
            income.Amount.Currency,
            await ToDopAsync(income.Amount, ct).ConfigureAwait(false));
    }

    private string NameOf(Guid? categoryId) =>
        categoryId is { } id && _categoryNames.TryGetValue(id, out string? name) ? name : string.Empty;

    private async Task<decimal?> ToDopAsync(Money amount, CancellationToken ct)
    {
        Result converted = await _converter.ConvertAsync(amount, Currency.DOP, ct).ConfigureAwait(false);
        return converted.IsSuccess ? ((Money)converted.Value!).Amount : null;
    }
}
