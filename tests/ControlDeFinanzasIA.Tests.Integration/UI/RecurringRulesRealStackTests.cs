using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateSubscription;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeactivateRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeleteRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.GenerateDueRecurring;
using ControlDeFinanzasIA.Application.UseCases.Recurring.SkipNextOccurrence;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateSubscription;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using ControlDeFinanzasIA.Tests.Integration.Persistence;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.UI;

/// <summary>
/// Real stack (SQLite, real repositories, real use cases, real view model) regression tests for editing a
/// recurring rule: saving an edit must never move the schedule (pre-1.0 slice C).
/// </summary>
public sealed class RecurringRulesRealStackTests : IDisposable
{
    private readonly TestAppDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed class Clock : ControlDeFinanzasIA.Application.Abstractions.IClock
    {
        public DateTime UtcNow => new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

        public DateOnly Today => new(2026, 9, 20);
    }

    private async Task<(RecurringRulesViewModel Vm, RecurringRuleRepository Rules)> NewAsync()
    {
        AppDbContext db = _factory.CreateContext();
        await db.Database.MigrateAsync();
        var rules = new RecurringRuleRepository(db);
        var vm = new RecurringRulesViewModel(
            rules,
            new CategoryRepository(db),
            new PaymentMethodRepository(db),
            new CreateSubscriptionUseCase(rules, new CreateSubscriptionValidator(), NullLogger<CreateSubscriptionUseCase>.Instance),
            new UpdateSubscriptionUseCase(rules, new UpdateSubscriptionValidator(), NullLogger<UpdateSubscriptionUseCase>.Instance),
            new CreateFixedExpenseUseCase(rules, new CreateFixedExpenseValidator(), NullLogger<CreateFixedExpenseUseCase>.Instance),
            new UpdateFixedExpenseUseCase(rules, new UpdateFixedExpenseValidator(), NullLogger<UpdateFixedExpenseUseCase>.Instance),
            new GenerateDueRecurringUseCase(rules, new ExpenseRepository(db), new Clock(), NullLogger<GenerateDueRecurringUseCase>.Instance),
            new DeactivateRecurringRuleUseCase(rules),
            new DeleteRecurringRuleUseCase(rules),
            new SkipNextOccurrenceUseCase(rules),
            new Clock(),
            NullLogger<RecurringRulesViewModel>.Instance);
        for (int i = 0; i < 300 && !vm.HasLoaded; i++)
        {
            await Task.Delay(10);
        }

        Assert.True(vm.HasLoaded);
        return (vm, rules);
    }

    private static async Task CreateAsync(RecurringRulesViewModel vm, string ruleType, int day)
    {
        vm.RuleType = ruleType;
        vm.Name = "Internet";
        vm.Amount = 30m;
        vm.Frequency = "Monthly";
        vm.DayOfMonth = day;
        vm.SelectedCategory = vm.Categories[0];
        vm.SelectedPaymentMethod = vm.PaymentMethods[0];
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, vm.FormError);
    }

    [Theory]
    [InlineData("Subscription")]
    [InlineData("FixedExpense")]
    public async Task Create_ThenEditingOnlyTheName_KeepsTheStoredScheduleDates(string ruleType)
    {
        (RecurringRulesViewModel vm, RecurringRuleRepository repo) = await NewAsync();
        await CreateAsync(vm, ruleType, 25);
        RecurringRule created = Assert.Single(await repo.GetAllAsync());
        Assert.Equal(new DateOnly(2026, 9, 25), created.NextGenerationDate);

        vm.EditCommand.Execute(vm.Rules[0]);
        vm.Name = "Internet Pro";
        await vm.SaveCommand.ExecuteAsync(null);

        RecurringRule after = Assert.Single(await repo.GetAllAsync());
        Assert.Equal("Internet Pro", after.Name);
        Assert.Equal(created.StartDate, after.StartDate);
        Assert.Equal(created.NextGenerationDate, after.NextGenerationDate);
    }

    [Fact]
    public async Task EditingARuleThatWasSkipped_KeepsItsAdvancedNextCharge_AndChangingTheDayMovesIt()
    {
        (RecurringRulesViewModel vm, RecurringRuleRepository repo) = await NewAsync();
        await CreateAsync(vm, "Subscription", 25);
        await vm.SkipCommand.ExecuteAsync(vm.Rules[0]);
        Assert.Equal(new DateOnly(2026, 10, 25), Assert.Single(await repo.GetAllAsync()).NextGenerationDate);

        vm.EditCommand.Execute(vm.Rules[0]);
        vm.Amount = 45m;
        await vm.SaveCommand.ExecuteAsync(null);
        RecurringRule unchanged = Assert.Single(await repo.GetAllAsync());
        Assert.Equal(45m, unchanged.Amount.Amount);
        Assert.Equal(new DateOnly(2026, 9, 25), unchanged.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 25), unchanged.NextGenerationDate);

        vm.EditCommand.Execute(vm.Rules[0]);
        vm.DayOfMonth = 28;
        await vm.SaveCommand.ExecuteAsync(null);
        RecurringRule moved = Assert.Single(await repo.GetAllAsync());
        Assert.Equal(new DateOnly(2026, 9, 28), moved.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 28), moved.NextGenerationDate);
    }

    [Fact]
    public async Task ADayThirtyOneRule_RoundTripsThroughTheDatabase_AndKeepsItsDay()
    {
        (RecurringRulesViewModel vm, RecurringRuleRepository repo) = await NewAsync();
        await CreateAsync(vm, "FixedExpense", 31);
        RecurringRule stored = Assert.Single(await repo.GetAllAsync());

        Assert.Equal(31, stored.StartDate.Day);
        Assert.Equal(new DateOnly(2026, 9, 30), stored.NextGenerationDate);
        vm.EditCommand.Execute(vm.Rules[0]);
        Assert.Equal(31, vm.DayOfMonth);
    }
}
