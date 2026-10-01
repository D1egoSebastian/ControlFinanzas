using ControlDeFinanzasIA.Application.UseCases.Incomes.GetIncomes;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Incomes.GetIncomes;

/// <summary>
/// The text search of the Income list: the query normalizes the text and hands it to the repository filter.
/// </summary>
public sealed class GetIncomesQueryTextTests
{
    [Theory]
    [InlineData("salario", "salario")]
    [InlineData("  bono anual ", "bono anual")]
    public async Task ExecuteAsync_WithText_PassesTheTrimmedTextToTheRepositoryFilter(string text, string expected)
    {
        var fake = new FakeIncomeRepository();
        var query = new GetIncomesQuery(fake, NullLogger<GetIncomesQuery>.Instance);

        await query.ExecuteAsync(new GetIncomesQueryRequest(1, 20, Text: text));

        Assert.NotNull(fake.LastIncomeFilter);
        Assert.Equal(expected, fake.LastIncomeFilter!.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithoutUsableText_AppliesNoTextFilter(string? text)
    {
        var fake = new FakeIncomeRepository();
        var query = new GetIncomesQuery(fake, NullLogger<GetIncomesQuery>.Instance);

        await query.ExecuteAsync(new GetIncomesQueryRequest(1, 20, Text: text));

        Assert.NotNull(fake.LastIncomeFilter);
        Assert.Null(fake.LastIncomeFilter!.Text);
    }
}
