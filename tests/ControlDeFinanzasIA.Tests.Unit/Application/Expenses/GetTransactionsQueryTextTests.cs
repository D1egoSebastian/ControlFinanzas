using ControlDeFinanzasIA.Application.UseCases.Expenses.GetTransactions;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Expenses;

/// <summary>
/// The text search of the Expenses list: the query normalizes the text and hands it to the repository filter.
/// </summary>
public sealed class GetTransactionsQueryTextTests
{
    [Theory]
    [InlineData("pizza", "pizza")]
    [InlineData("  gasolina shell  ", "gasolina shell")]
    public async Task ExecuteAsync_WithText_PassesTheTrimmedTextToTheRepositoryFilter(string text, string expected)
    {
        var fake = new FakeExpenseRepository();
        var query = new GetTransactionsQuery(fake, NullLogger<GetTransactionsQuery>.Instance);

        await query.ExecuteAsync(new GetTransactionsQueryRequest(1, 20, Text: text));

        Assert.NotNull(fake.LastExpenseFilter);
        Assert.Equal(expected, fake.LastExpenseFilter!.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithoutUsableText_AppliesNoTextFilter(string? text)
    {
        var fake = new FakeExpenseRepository();
        var query = new GetTransactionsQuery(fake, NullLogger<GetTransactionsQuery>.Instance);

        await query.ExecuteAsync(new GetTransactionsQueryRequest(1, 20, Text: text));

        Assert.NotNull(fake.LastExpenseFilter);
        Assert.Null(fake.LastExpenseFilter!.Text);
    }
}
