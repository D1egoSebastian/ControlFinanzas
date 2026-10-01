using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

public sealed class RecurringRuleRepositoryTests
{
    [Fact]
    public async Task MigrateAndRoundTrip_PersistsSubscription()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new RecurringRuleRepository(context);
        Subscription rule = RecurringRule.Create(
            "Netflix",
            Money.Create(15m, Currency.DOP),
            Currency.DOP,
            "Monthly",
            new DateOnly(2026, 1, 15),
            null,
            new Guid("11111111-1111-1111-1111-111111111111"),
            new Guid("22222222-2222-2222-2222-222222222222"));

        Application.Common.Result add = await repository.AddAsync(rule);
        RecurringRule? loaded = await repository.GetByIdAsync(rule.Id);

        Assert.True(add.IsSuccess);
        Assert.NotNull(loaded);
        Assert.IsType<Subscription>(loaded);
        Assert.Equal("Netflix", loaded.Name);
        Assert.Equal(15m, loaded.Amount.Amount);
    }

    [Fact]
    public async Task UpdateAsync_ChangesPersistedRule()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repository = new RecurringRuleRepository(context);
        Subscription rule = RecurringRule.Create(
            "Internet", Money.Create(30m, Currency.DOP), Currency.DOP, "Monthly",
            new DateOnly(2026, 1, 1), null, Guid.NewGuid(), Guid.NewGuid());

        await repository.AddAsync(rule);
        rule.Update("Internet Pro", Money.Create(40m, Currency.DOP), Currency.DOP,
            "Monthly", rule.StartDate, rule.EndDate, rule.CategoryId, rule.PaymentMethodId);

        Application.Common.Result update = await repository.UpdateAsync(rule);
        RecurringRule? loaded = await repository.GetByIdAsync(rule.Id);

        Assert.True(update.IsSuccess);
        Assert.Equal("Internet Pro", loaded!.Name);
        Assert.Equal(40m, loaded.Amount.Amount);
    }

    [Fact]
    public async Task UpdateAsync_FromADetachedCopy_PersistsTheAmountAndTheScheduleDates()
    {
        // The real app edits a copy read without tracking (GetByIdAsync), not the instance the context already holds.
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repository = new RecurringRuleRepository(context);
        Subscription rule = RecurringRule.Create(
            "Internet", Money.Create(30m, Currency.DOP), Currency.DOP, "Monthly",
            new DateOnly(2026, 8, 25), null, new Guid("11111111-1111-1111-1111-000000000001"), new Guid("22222222-2222-2222-2222-000000000001"));
        await repository.AddAsync(rule);

        RecurringRule copy = (await repository.GetByIdAsync(rule.Id))!;
        copy.Update("Internet Pro", Money.Create(45m, Currency.USD), Currency.USD, "Bimonthly",
            new DateOnly(2026, 9, 28), null, copy.CategoryId, copy.PaymentMethodId, null, new DateOnly(2026, 9, 28));
        Assert.True((await repository.UpdateAsync(copy)).IsSuccess);

        context.ChangeTracker.Clear();
        RecurringRule loaded = (await repository.GetByIdAsync(rule.Id))!;
        Assert.Equal("Internet Pro", loaded.Name);
        Assert.Equal(45m, loaded.Amount.Amount);
        Assert.Equal(Currency.USD, loaded.Amount.Currency);
        Assert.Equal("Bimonthly", loaded.Frequency);
        Assert.Equal(new DateOnly(2026, 9, 28), loaded.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 28), loaded.NextGenerationDate);
    }

    [Fact]
    public async Task GeneratedExpense_RoundTripsProvenanceAndPeriod()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var rules = new RecurringRuleRepository(context);
        var expenses = new ExpenseRepository(context);
        Subscription rule = RecurringRule.Create("Cloud", Money.Create(12m, Currency.USD), Currency.USD,
            "Monthly", new DateOnly(2026, 1, 15), null,
            new Guid("11111111-1111-1111-1111-000000000003"),
            new Guid("22222222-2222-2222-2222-000000000001"));
        await rules.AddAsync(rule);
        var expense = Expense.CreateGenerated(rule.CategoryId, new DateOnly(2026, 2, 15), rule.Amount,
            rule.Name, null, DateTime.UtcNow, rule.PaymentMethodId, rule.Id, new DateOnly(2026, 2, 1));

        Assert.True((await expenses.AddAsync(expense)).IsSuccess);
        Expense? loaded = await expenses.GetByRecurringRuleAndPeriodAsync(rule.Id, new DateOnly(2026, 2, 1));

        Assert.NotNull(loaded);
        Assert.True(loaded!.IsAutoGenerated);
        Assert.Equal(rule.Id, loaded.RecurringRuleId);
        Assert.Equal(new DateOnly(2026, 2, 1), loaded.RecurringPeriod);
    }
}
