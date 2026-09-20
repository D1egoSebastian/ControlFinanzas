using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetsForMonth;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Budgets;

/// <summary>
/// Tests for <see cref="GetBudgetsForMonthQuery"/> (D-5: reads the
/// repository directly, GetDebtsQuery shape).
/// </summary>
public sealed class GetBudgetsForMonthQueryTests
{
    [Fact]
    public async Task ExecuteAsync_BudgetsExistForMonth_ReturnsThem()
    {
        var repository = new FakeBudgetRepository();
        var september = Budget.Create(2026, 9, Guid.NewGuid(), Money.Create(10000m, Currency.DOP));
        var october = Budget.Create(2026, 10, Guid.NewGuid(), Money.Create(5000m, Currency.DOP));
        repository.Seed(september);
        repository.Seed(october);
        var query = new GetBudgetsForMonthQuery(repository);

        Result result = await query.ExecuteAsync(2026, 9);

        Assert.True(result.IsSuccess);
        IReadOnlyList<Budget> budgets = Assert.IsAssignableFrom<IReadOnlyList<Budget>>(result.Value);
        Budget only = Assert.Single(budgets);
        Assert.Equal(september.Id, only.Id);
    }

    [Fact]
    public async Task ExecuteAsync_NoBudgetsForMonth_ReturnsEmptyList()
    {
        var repository = new FakeBudgetRepository();
        var query = new GetBudgetsForMonthQuery(repository);

        Result result = await query.ExecuteAsync(2026, 9);

        Assert.True(result.IsSuccess);
        IReadOnlyList<Budget> budgets = Assert.IsAssignableFrom<IReadOnlyList<Budget>>(result.Value);
        Assert.Empty(budgets);
    }
}
