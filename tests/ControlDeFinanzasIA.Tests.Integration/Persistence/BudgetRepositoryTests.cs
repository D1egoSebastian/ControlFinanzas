using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integracion de round-trip para <see cref="BudgetRepository"/>
/// contra SQLite real (no in-memory provider), mismo patron que
/// <c>DebtRepositoryTests</c>.
/// </summary>
public sealed class BudgetRepositoryTests
{
    private static readonly Guid _categoryId = Guid.NewGuid();

    private static Budget CreateBudget(decimal amount = 15000m, int year = 2026, int month = 9, Guid? categoryId = null) =>
        Budget.Create(year, month, categoryId ?? _categoryId, Money.Create(amount, Currency.DOP));

    [Fact]
    public async Task AddAsync_ThenGetById_RoundTripsAmountAndCurrencyThroughOwnsOne()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new BudgetRepository(context);
        Budget budget = CreateBudget();

        Result addResult = await repository.AddAsync(budget);
        Budget? loaded = await repository.GetByIdAsync(budget.Id);

        Assert.True(addResult.IsSuccess);
        Assert.NotNull(loaded);
        Assert.Equal(15000m, loaded!.Amount.Amount);
        Assert.Equal(Currency.DOP, loaded.Amount.Currency);
        Assert.Equal(Currency.DOP, loaded.Currency);
        Assert.Equal(2026, loaded.Year);
        Assert.Equal(9, loaded.Month);
        Assert.Equal(_categoryId, loaded.CategoryId);
    }

    [Fact]
    public async Task UpdateAsync_AmountChange_PersistsTheNewAmount()
    {
        // Regression guard for the CurrentValues.SetValues owned-type gap (D-7):
        // a naive DebtRepository-style copy would silently discard this edit.
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new BudgetRepository(context);
        Budget budget = CreateBudget(amount: 15000m);
        await repository.AddAsync(budget);

        budget.ChangeAmount(Money.Create(9000m, Currency.DOP));
        Result updateResult = await repository.UpdateAsync(budget);

        await using Infrastructure.Persistence.AppDbContext freshContext = factory.CreateContext();
        var freshRepository = new BudgetRepository(freshContext);
        Budget? reloaded = await freshRepository.GetByIdAsync(budget.Id);

        Assert.True(updateResult.IsSuccess);
        Assert.NotNull(reloaded);
        Assert.Equal(9000m, reloaded!.Amount.Amount);
    }

    [Fact]
    public async Task UpdateAsync_CurrencyChange_PersistsTheNewCurrency()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new BudgetRepository(context);
        Budget budget = CreateBudget(amount: 15000m);
        await repository.AddAsync(budget);

        budget.ChangeAmount(Money.Create(300m, Currency.USD));
        await repository.UpdateAsync(budget);

        await using Infrastructure.Persistence.AppDbContext freshContext = factory.CreateContext();
        var freshRepository = new BudgetRepository(freshContext);
        Budget? reloaded = await freshRepository.GetByIdAsync(budget.Id);

        Assert.Equal(300m, reloaded!.Amount.Amount);
        Assert.Equal(Currency.USD, reloaded.Amount.Currency);
        Assert.Equal(Currency.USD, reloaded.Currency);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new BudgetRepository(context);
        Budget budget = CreateBudget();

        Result updateResult = await repository.UpdateAsync(budget);

        Assert.False(updateResult.IsSuccess);
        Assert.Equal("NotFound", updateResult.Error!.Code);
    }

    [Fact]
    public async Task RemoveAsync_ExistingBudget_DeletesTheRow()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new BudgetRepository(context);
        Budget budget = CreateBudget();
        await repository.AddAsync(budget);

        Result removeResult = await repository.RemoveAsync(budget.Id);
        Budget? loaded = await repository.GetByIdAsync(budget.Id);

        Assert.True(removeResult.IsSuccess);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task RemoveAsync_UnknownId_ReturnsNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new BudgetRepository(context);

        Result removeResult = await repository.RemoveAsync(Guid.NewGuid());

        Assert.False(removeResult.IsSuccess);
        Assert.Equal("NotFound", removeResult.Error!.Code);
    }

    [Fact]
    public async Task UniqueIndex_DuplicateYearMonthCategory_ThrowsOnSave()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new BudgetRepository(context);
        Budget first = CreateBudget(amount: 10000m, year: 2026, month: 9);
        await repository.AddAsync(first);

        Budget duplicate = CreateBudget(amount: 5000m, year: 2026, month: 9);

        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
            () => repository.AddAsync(duplicate));
    }

    [Fact]
    public async Task GetByMonthAsync_ExcludesOtherMonths()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new BudgetRepository(context);
        Budget september = CreateBudget(amount: 15000m, year: 2026, month: 9, categoryId: Guid.NewGuid());
        Budget october = CreateBudget(amount: 12000m, year: 2026, month: 10, categoryId: Guid.NewGuid());
        await repository.AddAsync(september);
        await repository.AddAsync(october);

        IReadOnlyList<Budget> results = await repository.GetByMonthAsync(2026, 9);

        Assert.Single(results);
        Assert.Equal(september.Id, results[0].Id);
    }

    [Fact]
    public async Task GetByCategoryAndMonthAsync_ExistingBudget_ReturnsIt()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new BudgetRepository(context);
        Budget budget = CreateBudget(categoryId: _categoryId);
        await repository.AddAsync(budget);

        Budget? found = await repository.GetByCategoryAndMonthAsync(_categoryId, 2026, 9);

        Assert.NotNull(found);
        Assert.Equal(budget.Id, found!.Id);
    }

    [Fact]
    public async Task GetByCategoryAndMonthAsync_Absent_ReturnsNull()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new BudgetRepository(context);

        Budget? found = await repository.GetByCategoryAndMonthAsync(Guid.NewGuid(), 2026, 9);

        Assert.Null(found);
    }
}
