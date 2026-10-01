using System.Globalization;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateSubscription;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeactivateRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeleteRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.GenerateDueRecurring;
using ControlDeFinanzasIA.Application.UseCases.Recurring.SkipNextOccurrence;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateSubscription;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Pre-1.0 slice C: the REAL <see cref="RecurringRulesViewModel"/> over the REAL use cases (only the repositories are
/// doubles). A recurring charge is defined by the day of the month it always happens on; the first charge is derived
/// and shown read-only; editing a rule never moves its schedule unless the user changes the schedule.
/// </summary>
public sealed class RecurringRulesViewModelScheduleTests
{
    internal static readonly Guid CategoryId = new("11111111-1111-1111-1111-000000000001");
    internal static readonly Guid MethodId = new("22222222-2222-2222-2222-000000000001");
    private static readonly DateOnly _sep20 = new(2026, 9, 20);

    internal sealed record Rig(RecurringRulesViewModel Vm, FakeRecurringRuleRepository Rules, FakeExpenseRepository Expenses, FakeClock Clock);

    internal static async Task<Rig> NewAsync(DateOnly today, params RecurringRule[] seeded)
    {
        var rules = new FakeRecurringRuleRepository();
        foreach (RecurringRule rule in seeded)
        {
            rules.Seed(rule);
        }

        var expenses = new FakeExpenseRepository();
        var clock = new FakeClock { Today = today, UtcNow = today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc) };
        var categories = new FakeCategoryRepository();
        categories.SetCategories([new Category { Id = CategoryId, Name = "Servicios", IsSystem = false }]);
        var methods = new FakePaymentMethodRepository
        {
            Methods = [new PaymentMethod { Id = MethodId, Name = "Tarjeta", IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }],
        };
        var vm = new RecurringRulesViewModel(
            rules,
            categories,
            methods,
            new CreateSubscriptionUseCase(rules, new CreateSubscriptionValidator(), NullLogger<CreateSubscriptionUseCase>.Instance),
            new UpdateSubscriptionUseCase(rules, new UpdateSubscriptionValidator(), NullLogger<UpdateSubscriptionUseCase>.Instance),
            new CreateFixedExpenseUseCase(rules, new CreateFixedExpenseValidator(), NullLogger<CreateFixedExpenseUseCase>.Instance),
            new UpdateFixedExpenseUseCase(rules, new UpdateFixedExpenseValidator(), NullLogger<UpdateFixedExpenseUseCase>.Instance),
            new GenerateDueRecurringUseCase(rules, expenses, clock, NullLogger<GenerateDueRecurringUseCase>.Instance),
            new DeactivateRecurringRuleUseCase(rules),
            new DeleteRecurringRuleUseCase(rules),
            new SkipNextOccurrenceUseCase(rules),
            clock,
            NullLogger<RecurringRulesViewModel>.Instance);
        for (int i = 0; i < 400 && !vm.HasLoaded; i++)
        {
            await Task.Delay(5);
        }

        Assert.True(vm.HasLoaded);
        return new Rig(vm, rules, expenses, clock);
    }

    private static Subscription Rule(string name, DateOnly start, string frequency = "Monthly") =>
        RecurringRule.Create(name, Money.Create(30m, Currency.DOP), Currency.DOP, frequency, start, null, CategoryId, MethodId);

    private static void Fill(RecurringRulesViewModel vm, string name = "Internet")
    {
        vm.Name = name;
        vm.Amount = 30m;
        vm.SelectedCategory = vm.Categories[0];
        vm.SelectedPaymentMethod = vm.PaymentMethods[0];
    }

    // The next charge is written with the month name so "11/9" can never be read the wrong way round.
    private static string Short(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.CurrentUICulture);

    [Theory]
    [InlineData("Subscription", 25)]
    [InlineData("FixedExpense", 25)]
    [InlineData("Subscription", 11)]
    public async Task Create_Monthly_StoresTheFirstChargeOfTheChosenDay(string type, int day)
    {
        Rig rig = await NewAsync(_sep20);
        RecurringRulesViewModel vm = rig.Vm;
        Fill(vm);
        vm.RuleType = type;
        vm.Frequency = "Monthly";
        vm.DayOfMonth = day;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.FormError);
        RecurringRule stored = Assert.Single(rig.Rules.Added);
        DateOnly expected = day == 25 ? new DateOnly(2026, 9, 25) : new DateOnly(2026, 10, 11);
        Assert.Equal(expected, stored.StartDate);
        Assert.Equal(expected, stored.NextGenerationDate);
        Assert.Equal(type == "FixedExpense", stored is FixedExpense);
    }

    [Fact]
    public async Task Create_DayThirtyOne_InAShortMonth_IsTheLastDayThisMonth_AndTheThirtyFirstAfterwards()
    {
        Rig rig = await NewAsync(new DateOnly(2026, 11, 15));
        Fill(rig.Vm);
        rig.Vm.DayOfMonth = 31;

        await rig.Vm.SaveCommand.ExecuteAsync(null);

        RecurringRule stored = Assert.Single(rig.Rules.Added);
        Assert.Equal(new DateOnly(2026, 11, 30), stored.NextGenerationDate);
        Assert.Equal(31, stored.StartDate.Day);

        rig.Clock.Today = new DateOnly(2026, 11, 30);
        await rig.Vm.ConfirmGenerationCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 11, 30), Assert.Single(rig.Expenses.AddedExpenses).Date);
        Assert.Equal(new DateOnly(2026, 12, 31), stored.NextGenerationDate);
    }

    [Fact]
    public async Task Create_Yearly_UsesTheMonthAndTheDay()
    {
        Rig rig = await NewAsync(_sep20);
        Fill(rig.Vm);
        rig.Vm.Frequency = "Yearly";
        rig.Vm.Month = 3;
        rig.Vm.DayOfMonth = 15;

        await rig.Vm.SaveCommand.ExecuteAsync(null);

        RecurringRule stored = Assert.Single(rig.Rules.Added);
        Assert.Equal(new DateOnly(2027, 3, 15), stored.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 15), stored.NextGenerationDate);
    }

    [Fact]
    public async Task Create_Bimonthly_UsesTheChosenDay()
    {
        Rig rig = await NewAsync(_sep20);
        Fill(rig.Vm);
        rig.Vm.Frequency = "Bimonthly";
        rig.Vm.DayOfMonth = 5;

        await rig.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 10, 5), Assert.Single(rig.Rules.Added).NextGenerationDate);
    }

    [Fact]
    public async Task TheFormStartsOnTodaysDay_AndShowsTheDerivedNextChargeReadOnly()
    {
        Rig rig = await NewAsync(_sep20);
        RecurringRulesViewModel vm = rig.Vm;

        Assert.Equal(20, vm.DayOfMonth);
        Assert.Contains(Short(new DateOnly(2026, 9, 20)), vm.NextChargeText, StringComparison.Ordinal);

        vm.DayOfMonth = 11;

        Assert.Contains(Short(new DateOnly(2026, 10, 11)), vm.NextChargeText, StringComparison.Ordinal);
        Assert.Equal(Enumerable.Range(1, 31), vm.DaysOfMonth);
        Assert.Equal(12, vm.Months.Count);
    }

    [Theory]
    [InlineData("Monthly", false)]
    [InlineData("Bimonthly", false)]
    [InlineData("Yearly", true)]
    public async Task TheMonthControlIsOnlyForYearlyCharges_AndTheDayControlIsAlwaysThere(string frequency, bool month)
    {
        Rig rig = await NewAsync(_sep20);

        rig.Vm.Frequency = frequency;

        Assert.Equal(month, rig.Vm.IsMonthVisible);
    }

    [Fact]
    public async Task TheTypeComesWithPlainLanguageHelp_ThatFollowsTheChoice()
    {
        Rig rig = await NewAsync(_sep20);
        rig.Vm.RuleType = "Subscription";
        string subscription = rig.Vm.RuleTypeHelp;

        rig.Vm.RuleType = "FixedExpense";

        Assert.NotEmpty(subscription);
        Assert.NotEmpty(rig.Vm.RuleTypeHelp);
        Assert.NotEqual(subscription, rig.Vm.RuleTypeHelp);
    }

    [Fact]
    public async Task AfterCreating_TheFormIsReadyForTheNextRule_SoSavingTwiceCannotDuplicate()
    {
        Rig rig = await NewAsync(_sep20);
        Fill(rig.Vm);

        await rig.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, rig.Vm.Name);
        Assert.Null(rig.Vm.SelectedCategory);
        Assert.Equal(20, rig.Vm.DayOfMonth);
        Assert.Single(rig.Vm.Rules);
    }

    // ---- editing never moves the schedule (the reported bug) ----

    [Theory]
    [InlineData("Subscription")]
    [InlineData("FixedExpense")]
    public async Task Edit_OnlyTheName_OfARuleWhoseDayHasNotComeThisMonth_KeepsTheStoredSchedule(string type)
    {
        // Created with last month's date; this month's occurrence (the 25th) is still in the future.
        var start = new DateOnly(2026, 8, 25);
        RecurringRule rule = type == "FixedExpense"
            ? new FixedExpense(Guid.NewGuid(), "Internet", Money.Create(30m, Currency.DOP), Currency.DOP, "Monthly", start, null, CategoryId, MethodId, [], true, start)
            : Rule("Internet", start);
        Rig rig = await NewAsync(_sep20, rule);

        rig.Vm.EditCommand.Execute(rig.Vm.Rules[0]);
        rig.Vm.Name = "Internet Pro";
        await rig.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, rig.Vm.FormError);
        Assert.Equal("Internet Pro", rule.Name);
        Assert.Equal(start, rule.StartDate);
        Assert.Equal(start, rule.NextGenerationDate);
    }

    [Fact]
    public async Task Edit_WithNoChanges_KeepsTheScheduleOfAnAlreadyAdvancedRule()
    {
        Subscription rule = Rule("Internet", new DateOnly(2026, 8, 25));
        rule.AdvanceAfterGeneration(new DateOnly(2026, 8, 25), 1);
        Rig rig = await NewAsync(_sep20, rule);

        rig.Vm.EditCommand.Execute(rig.Vm.Rules[0]);
        Assert.Contains(Short(new DateOnly(2026, 9, 25)), rig.Vm.NextChargeText, StringComparison.Ordinal);
        await rig.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 8, 25), rule.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 25), rule.NextGenerationDate);
    }

    [Fact]
    public async Task Edit_OnlyTheAmount_KeepsTheSchedule()
    {
        Subscription rule = Rule("Internet", new DateOnly(2026, 8, 25));
        Rig rig = await NewAsync(_sep20, rule);

        rig.Vm.EditCommand.Execute(rig.Vm.Rules[0]);
        rig.Vm.Amount = 45m;
        await rig.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(45m, rule.Amount.Amount);
        Assert.Equal(new DateOnly(2026, 8, 25), rule.StartDate);
        Assert.Equal(new DateOnly(2026, 8, 25), rule.NextGenerationDate);
    }

    [Fact]
    public async Task Edit_TheDay_MovesTheScheduleToTheNewDay()
    {
        Subscription rule = Rule("Internet", new DateOnly(2026, 8, 25));
        rule.AdvanceAfterGeneration(new DateOnly(2026, 8, 25), 1);
        rule.AdvanceAfterGeneration(new DateOnly(2026, 9, 25), 1);
        Rig rig = await NewAsync(_sep20, rule);

        rig.Vm.EditCommand.Execute(rig.Vm.Rules[0]);
        Assert.Equal(25, rig.Vm.DayOfMonth);
        rig.Vm.DayOfMonth = 28;
        Assert.Contains(Short(new DateOnly(2026, 9, 28)), rig.Vm.NextChargeText, StringComparison.Ordinal);
        await rig.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 9, 28), rule.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 28), rule.NextGenerationDate);
    }

    [Fact]
    public async Task Edit_TheFrequencyToYearly_AsksForTheMonth_AndReschedules()
    {
        Subscription rule = Rule("Seguro", new DateOnly(2026, 8, 25));
        Rig rig = await NewAsync(_sep20, rule);

        rig.Vm.EditCommand.Execute(rig.Vm.Rules[0]);
        rig.Vm.Frequency = "Yearly";
        rig.Vm.Month = 12;
        await rig.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Yearly", rule.Frequency);
        Assert.Equal(new DateOnly(2026, 12, 25), rule.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 25), rule.NextGenerationDate);
    }

    [Fact]
    public async Task Edit_ALegacyYearlyRule_ShowsItsMonthAndDay_AndSavesUnchanged()
    {
        Subscription rule = Rule("Dominio", new DateOnly(2026, 1, 15), "Yearly");
        Rig rig = await NewAsync(_sep20, rule);

        rig.Vm.EditCommand.Execute(rig.Vm.Rules[0]);
        Assert.Equal(1, rig.Vm.Month);
        Assert.Equal(15, rig.Vm.DayOfMonth);
        Assert.True(rig.Vm.IsMonthVisible);
        await rig.Vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 1, 15), rule.StartDate);
        Assert.Equal(new DateOnly(2026, 1, 15), rule.NextGenerationDate);
    }

    [Fact]
    public async Task Edit_ALegacyDayThirtyRule_ShowsThatDay()
    {
        Subscription rule = Rule("Gym", new DateOnly(2026, 1, 30));
        Rig rig = await NewAsync(_sep20, rule);

        rig.Vm.EditCommand.Execute(rig.Vm.Rules[0]);

        Assert.Equal(30, rig.Vm.DayOfMonth);
    }

    // ---- skip and generation semantics ----

    [Fact]
    public async Task Skip_MovesOnlyTheRowThatWasClicked_NotTheRuleBeingEdited()
    {
        Subscription edited = Rule("Internet", new DateOnly(2026, 8, 11));
        Subscription other = Rule("Gym", new DateOnly(2026, 8, 20));
        Rig rig = await NewAsync(_sep20, edited, other);
        rig.Vm.EditCommand.Execute(edited);

        await rig.Vm.SkipCommand.ExecuteAsync(other);

        Assert.Equal(new DateOnly(2026, 8, 11), edited.NextGenerationDate);
        Assert.Equal(new DateOnly(2026, 9, 20), other.NextGenerationDate);
        Assert.Equal(string.Empty, rig.Vm.FormError);
    }

    [Fact]
    public async Task Skip_WithoutARule_DoesNothing()
    {
        Subscription rule = Rule("Internet", new DateOnly(2026, 8, 11));
        Rig rig = await NewAsync(_sep20, rule);

        await rig.Vm.SkipCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 8, 11), rule.NextGenerationDate);
    }

    [Fact]
    public async Task Preview_AndConfirm_GenerateTheChosenDayOfTheCurrentMonth_AndRefreshTheList()
    {
        Subscription rule = Rule("Internet", new DateOnly(2026, 9, 25));
        Rig rig = await NewAsync(_sep20, rule);
        int loadsBefore = rig.Rules.GetAllCallCount;

        await rig.Vm.PreviewGenerationCommand.ExecuteAsync(null);
        RecurringGenerationPreview preview = Assert.Single(rig.Vm.PreviewItems);
        Assert.Equal(new DateOnly(2026, 9, 25), preview.OccurrenceDate);

        await rig.Vm.ConfirmGenerationCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 9, 25), Assert.Single(rig.Expenses.AddedExpenses).Date);
        Assert.Equal(new DateOnly(2026, 10, 25), rule.NextGenerationDate);
        Assert.True(rig.Rules.GetAllCallCount > loadsBefore, "The list must be reloaded after generating.");
        Assert.Equal(new DateOnly(2026, 10, 25), Assert.Single(rig.Vm.Rules).NextGenerationDate);
    }
}
