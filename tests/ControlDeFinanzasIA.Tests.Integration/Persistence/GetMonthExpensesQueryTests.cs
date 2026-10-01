using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthExpenses;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// <see cref="GetMonthExpensesQuery"/> against real SQLite and the real repositories: month bounds, ordering,
/// the cap with the full count, and the category names.
/// </summary>
public sealed class GetMonthExpensesQueryTests
{
    private static readonly DateTime _timestamp = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_RealSqlite_ReturnsTheMonthsLatestExpenses_WithNamesCountAndOrder()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var expenses = new ExpenseRepository(context);
        var categories = new CategoryRepository(context);
        Category category = (await categories.GetAllAsync())[0];

        await expenses.AddAsync(Create(category.Id, new DateOnly(2026, 2, 28), 999m, "Febrero", Currency.DOP));
        await expenses.AddAsync(Create(category.Id, new DateOnly(2026, 3, 1), 10m, "Primer dia", Currency.DOP));
        await expenses.AddAsync(Create(category.Id, new DateOnly(2026, 3, 15), 20m, "Mitad", Currency.USD));
        await expenses.AddAsync(Create(category.Id, new DateOnly(2026, 3, 31), 30m, "Ultimo dia", Currency.DOP));
        await expenses.AddAsync(Create(category.Id, new DateOnly(2026, 4, 1), 999m, "Abril", Currency.DOP));

        var query = new GetMonthExpensesQuery(expenses, categories, new SixtyPesoConverter());

        Result all = await query.ExecuteAsync(new DateOnly(2026, 3, 9));
        MonthExpensesResult month = Assert.IsType<MonthExpensesResult>(all.Value);

        Assert.Equal(3, month.TotalCount);
        Assert.Equal(new[] { "Ultimo dia", "Mitad", "Primer dia" }, month.Items.Select(i => i.Description).ToArray());
        Assert.All(month.Items, i => Assert.Equal(category.Name, i.CategoryName));
        Assert.Equal(1200m, month.Items[1].AmountDop);

        MonthExpensesResult capped = Assert.IsType<MonthExpensesResult>((await query.ExecuteAsync(new DateOnly(2026, 3, 9), limit: 2)).Value);

        Assert.Equal(3, capped.TotalCount);
        Assert.Equal(new[] { "Ultimo dia", "Mitad" }, capped.Items.Select(i => i.Description).ToArray());
    }

    private static Expense Create(Guid categoryId, DateOnly date, decimal amount, string description, Currency currency) =>
        Expense.Create(categoryId, date, Money.Create(amount, currency), description, null, _timestamp);

    private sealed class SixtyPesoConverter : ICurrencyConverter
    {
        public Task<Result> ConvertAsync(Money source, Currency target, CancellationToken ct = default) =>
            Task.FromResult(Result.Success(source.Currency == target ? source : Money.Create(source.Amount * 60m, target)));

        public Task<MoneyDisplay> DisplayAsync(Money source, CancellationToken ct = default) =>
            Task.FromResult(new MoneyDisplay(source, null, null));
    }
}
