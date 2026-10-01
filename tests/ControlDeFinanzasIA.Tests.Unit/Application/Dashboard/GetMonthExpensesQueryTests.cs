using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthExpenses;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Dashboard;

/// <summary>
/// Unit tests for <see cref="GetMonthExpensesQuery"/>: the Dashboard's "expenses of the month" list.
/// </summary>
public sealed class GetMonthExpensesQueryTests
{
    private static readonly Guid _foodId = Guid.NewGuid();
    private static readonly Guid _rentId = Guid.NewGuid();

    private static (GetMonthExpensesQuery Query, FakeExpenseRepository Repo, FakeCurrencyConverter Converter) Build(
        FakeCurrencyConverter? converter = null)
    {
        FakeExpenseRepository repo = new() { PageAddedItems = true };
        FakeCategoryRepository categories = new();
        categories.SetCategories(
        [
            new Category { Id = _foodId, Name = "Comida" },
            new Category { Id = _rentId, Name = "Alquiler" },
        ]);
        converter ??= new FakeCurrencyConverter();
        return (new GetMonthExpensesQuery(repo, categories, converter), repo, converter);
    }

    private static Task<Result> AddAsync(FakeExpenseRepository repo, Guid categoryId, DateOnly date, decimal amount, string description, Currency currency = Currency.DOP, int createdMinute = 0)
        => repo.AddAsync(Expense.Create(
            categoryId,
            date,
            Money.Create(amount, currency),
            description,
            null,
            new DateTime(2026, 1, 1, 12, createdMinute, 0, DateTimeKind.Utc)));

    [Fact]
    public async Task ExecuteAsync_ReturnsOnlyTheMonthsExpenses_MostRecentFirst_WithCategoryNames()
    {
        (GetMonthExpensesQuery query, FakeExpenseRepository repo, _) = Build();
        await AddAsync(repo, _foodId, new DateOnly(2026, 3, 5), 100m, "Supermercado");
        await AddAsync(repo, _rentId, new DateOnly(2026, 3, 20), 900m, "Renta");
        await AddAsync(repo, _foodId, new DateOnly(2026, 2, 28), 55m, "Febrero");
        await AddAsync(repo, _foodId, new DateOnly(2026, 4, 1), 77m, "Abril");

        Result result = await query.ExecuteAsync(new DateOnly(2026, 3, 1));

        MonthExpensesResult month = Assert.IsType<MonthExpensesResult>(result.Value);
        Assert.Equal(2, month.TotalCount);
        Assert.Equal(new[] { "Renta", "Supermercado" }, month.Items.Select(i => i.Description).ToArray());
        Assert.Equal(new[] { "Alquiler", "Comida" }, month.Items.Select(i => i.CategoryName).ToArray());
        Assert.Equal(new DateOnly(2026, 3, 20), month.Items[0].Date);
        Assert.Equal(900m, month.Items[0].Amount.Amount);
        Assert.False(month.HasMissingRates);
    }

    [Fact]
    public async Task ExecuteAsync_MoreThanTheLimit_ReturnsTheLatestOnly_AndTheFullCount()
    {
        (GetMonthExpensesQuery query, FakeExpenseRepository repo, _) = Build();
        for (int day = 1; day <= 12; day++)
        {
            await AddAsync(repo, _foodId, new DateOnly(2026, 3, day), day, $"Gasto {day}");
        }

        Result result = await query.ExecuteAsync(new DateOnly(2026, 3, 1), limit: 5);

        MonthExpensesResult month = Assert.IsType<MonthExpensesResult>(result.Value);
        Assert.Equal(12, month.TotalCount);
        Assert.Equal(5, month.Items.Count);
        Assert.Equal("Gasto 12", month.Items[0].Description);
        Assert.Equal("Gasto 8", month.Items[^1].Description);
    }

    [Fact]
    public async Task ExecuteAsync_SameDay_BreaksTiesByCreationTimeDescending()
    {
        (GetMonthExpensesQuery query, FakeExpenseRepository repo, _) = Build();
        DateOnly day = new(2026, 3, 9);
        await AddAsync(repo, _foodId, day, 10m, "Primero", createdMinute: 1);
        await AddAsync(repo, _foodId, day, 20m, "Segundo", createdMinute: 2);

        Result result = await query.ExecuteAsync(new DateOnly(2026, 3, 1));

        MonthExpensesResult month = Assert.IsType<MonthExpensesResult>(result.Value);
        Assert.Equal(new[] { "Segundo", "Primero" }, month.Items.Select(i => i.Description).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_ForeignCurrency_CarriesTheDopEquivalent_AndFlagsAMissingRate()
    {
        FakeCurrencyConverter converter = new((source, target) =>
            source.Currency == Currency.USD
                ? Result.Success(Money.Create(source.Amount * 60m, target))
                : Result.Failure("ExchangeRateUnavailable", "no rate"));
        (GetMonthExpensesQuery query, FakeExpenseRepository repo, _) = Build(converter);
        await AddAsync(repo, _foodId, new DateOnly(2026, 3, 2), 10m, "Dolares", Currency.USD);
        await AddAsync(repo, _foodId, new DateOnly(2026, 3, 1), 5m, "Pesos", Currency.DOP);

        Result result = await query.ExecuteAsync(new DateOnly(2026, 3, 1));

        MonthExpensesResult month = Assert.IsType<MonthExpensesResult>(result.Value);
        Assert.Equal(600m, month.Items[0].AmountDop);
        Assert.Equal(5m, month.Items[1].AmountDop); // same currency short-circuits to the amount itself

        FakeCurrencyConverter failing = new((_, _) => Result.Failure("ExchangeRateUnavailable", "no rate"));
        (GetMonthExpensesQuery failingQuery, FakeExpenseRepository failingRepo, _) = Build(failing);
        await AddAsync(failingRepo, _foodId, new DateOnly(2026, 3, 2), 10m, "Dolares", Currency.USD);

        MonthExpensesResult missing = Assert.IsType<MonthExpensesResult>((await failingQuery.ExecuteAsync(new DateOnly(2026, 3, 1))).Value);
        Assert.Null(missing.Items[0].AmountDop);
        Assert.True(missing.HasMissingRates);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCategory_FallsBackToAnEmptyName()
    {
        (GetMonthExpensesQuery query, FakeExpenseRepository repo, _) = Build();
        await AddAsync(repo, Guid.NewGuid(), new DateOnly(2026, 3, 2), 10m, "Huerfano");

        MonthExpensesResult month = Assert.IsType<MonthExpensesResult>((await query.ExecuteAsync(new DateOnly(2026, 3, 1))).Value);

        Assert.Equal(string.Empty, month.Items[0].CategoryName);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyMonth_IsSuccessWithZeroCount()
    {
        (GetMonthExpensesQuery query, FakeExpenseRepository repo, _) = Build();
        await AddAsync(repo, _foodId, new DateOnly(2026, 2, 2), 10m, "Otro mes");

        Result result = await query.ExecuteAsync(new DateOnly(2026, 3, 1));

        Assert.True(result.IsSuccess);
        MonthExpensesResult month = Assert.IsType<MonthExpensesResult>(result.Value);
        Assert.Equal(0, month.TotalCount);
        Assert.Empty(month.Items);
    }

    [Fact]
    public async Task ExecuteAsync_RepositoryThrows_ReturnsAFailureResultInsteadOfThrowing()
    {
        (GetMonthExpensesQuery query, FakeExpenseRepository repo, _) = Build();
        repo.SetThrowOnGetFilter(new InvalidOperationException("db down"));

        Result result = await query.ExecuteAsync(new DateOnly(2026, 3, 1));

        Assert.False(result.IsSuccess);
        Assert.Equal("RepositoryError", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_NonPositiveLimit_Throws()
    {
        (GetMonthExpensesQuery query, _, _) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => query.ExecuteAsync(new DateOnly(2026, 3, 1), limit: 0));
    }
}
