using System.Globalization;
using System.Text.RegularExpressions;
using ControlDeFinanzasIA.Application.UseCases.Debts.CreateDebtWithSchedule;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebts;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebtSchedule;
using ControlDeFinanzasIA.Application.UseCases.Debts.RegisterDebtPayment;
using ControlDeFinanzasIA.Application.UseCases.Debts.SkipDebtPayment;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateSubscription;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeactivateRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeleteRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.GenerateDueRecurring;
using ControlDeFinanzasIA.Application.UseCases.Recurring.SkipNextOccurrence;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateSubscription;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.Packaging;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S6 (R-19, Reconciliation #14): every status and error message the REAL <see cref="DebtsViewModel"/> and
/// <see cref="RecurringRulesViewModel"/> produce themselves resolves through <see cref="Strings"/> in both cultures.
/// Only the repositories around the view models are doubles.
/// </summary>
public sealed class DebtsRecurringLocalizationTests
{
    private static readonly Guid _categoryId = new("22222222-2222-2222-2222-000000000001");
    private static readonly Guid _methodId = new("22222222-2222-2222-2222-000000000002");

    public static TheoryData<string> Cultures => new() { "es-DO", "en" };

    private static async Task<T> InCulture<T>(string culture, Func<Task<T>> body)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return await body();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static Debt NewDebt(int installments = 1) =>
        Debt.Create(
            "Deuda",
            Money.Create(500m * installments, Currency.DOP),
            Currency.DOP,
            installments,
            Money.Create(500m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 1));

    private static async Task<(DebtsViewModel Vm, FakeDebtRepository Debts)> DebtsWithOneSelected()
    {
        Debt debt = NewDebt(2);
        var debts = new FakeDebtRepository();
        debts.Seed(debt);
        var payments = new FakeDebtPaymentRepository();
        payments.SeedRange(new DebtScheduleGenerator().Generate(debt));
        DebtsViewModel vm = DebtsViewModelTests.CreateViewModel(debts, payments);
        await Task.Delay(1);
        vm.SelectedDebt = vm.Debts.Single();
        await Task.Delay(1);
        return (vm, debts);
    }

    internal sealed record RecurringRig(RecurringRulesViewModel Vm, FakeRecurringRuleRepository Rules);

    internal static async Task<RecurringRig> NewRecurringRig(RecurringRule? seeded = null, bool throwOnLoad = false, TaskCompletionSource? gate = null, bool delay = true)
    {
        var rules = new FakeRecurringRuleRepository();
        if (seeded is not null)
        {
            rules.Seed(seeded);
        }

        if (throwOnLoad)
        {
            rules.SetThrowOnGetAll(new InvalidOperationException("boom"));
        }

        rules.GetAllGate = gate;
        var categories = new FakeCategoryRepository();
        categories.SetCategories([new Category { Id = _categoryId, Name = "Streaming", IsSystem = false }]);
        var methods = new FakePaymentMethodRepository
        {
            Methods = [new PaymentMethod { Id = _methodId, Name = "Card", IsSystem = false }],
        };
        var clock = new FakeClock { Today = new DateOnly(2026, 9, 20), UtcNow = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc) };

        var vm = new RecurringRulesViewModel(
            rules,
            categories,
            methods,
            new CreateSubscriptionUseCase(rules, new CreateSubscriptionValidator(), NullLogger<CreateSubscriptionUseCase>.Instance),
            new UpdateSubscriptionUseCase(rules, new UpdateSubscriptionValidator(), NullLogger<UpdateSubscriptionUseCase>.Instance),
            new CreateFixedExpenseUseCase(rules, new CreateFixedExpenseValidator(), NullLogger<CreateFixedExpenseUseCase>.Instance),
            new UpdateFixedExpenseUseCase(rules, new UpdateFixedExpenseValidator(), NullLogger<UpdateFixedExpenseUseCase>.Instance),
            new GenerateDueRecurringUseCase(rules, new FakeExpenseRepository(), clock, NullLogger<GenerateDueRecurringUseCase>.Instance),
            new DeactivateRecurringRuleUseCase(rules),
            new DeleteRecurringRuleUseCase(rules),
            new SkipNextOccurrenceUseCase(rules),
            NullLogger<RecurringRulesViewModel>.Instance);
        if (delay)
        {
            await Task.Delay(1);
        }

        return new RecurringRig(vm, rules);
    }

    private static RecurringRule NewRule() =>
        RecurringRule.Create("Netflix", Money.Create(15m, Currency.DOP), Currency.DOP, "Monthly", new DateOnly(2026, 1, 1), null, _categoryId, _methodId);

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Debts_StartDateRequired_ResolvesThroughStrings(string culture)
    {
        (string actual, string expected) = await InCulture(culture, async () =>
        {
            DebtsViewModel vm = DebtsViewModelTests.CreateViewModel(new FakeDebtRepository(), new FakeDebtPaymentRepository());
            vm.StartDate = null;
            await vm.CreateCommand.ExecuteAsync(null);
            return (vm.FormError, Strings.DebtsStartDateRequired);
        });

        Assert.Equal(expected, actual);
        Assert.Equal(culture == "en" ? "Select the start date." : "Selecciona la fecha de inicio.", actual);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Debts_SuccessfulCreate_ReportsTheLocalizedStatus(string culture)
    {
        (string actual, string expected) = await InCulture(culture, async () =>
        {
            DebtsViewModel vm = DebtsViewModelTests.CreateViewModel(new FakeDebtRepository(), new FakeDebtPaymentRepository());
            vm.Description = "Prestamo";
            vm.OriginalAmount = 3000m;
            vm.TotalInstallments = 3;
            vm.InstallmentAmount = 1000m;
            vm.StartDate = new DateTime(2026, 1, 15);
            await vm.CreateCommand.ExecuteAsync(null);
            return (vm.StatusMessage, Strings.DebtsCreated);
        });

        Assert.Equal(expected, actual);
        Assert.Equal(culture == "en" ? "Debt created with its installment plan." : "Deuda creada correctamente con su plan de cuotas.", actual);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Debts_RegisterAndSkipPayment_ReportTheLocalizedStatuses(string culture)
    {
        (string registered, string skipped, string expectedRegistered, string expectedSkipped) = await InCulture(culture, async () =>
        {
            (DebtsViewModel vm, _) = await DebtsWithOneSelected();
            await vm.RegisterPaymentCommand.ExecuteAsync(vm.Schedule.First());
            string registeredMessage = vm.StatusMessage;
            await vm.SkipPaymentCommand.ExecuteAsync(vm.Schedule.Last());
            return (registeredMessage, vm.StatusMessage, Strings.DebtsPaymentRegistered, Strings.DebtsSkipped);
        });

        Assert.Equal(expectedRegistered, registered);
        Assert.Equal(expectedSkipped, skipped);
        Assert.NotEqual(registered, skipped);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Debts_ALoadThatThrows_ReportsTheLocalizedLoadFailure(string culture)
    {
        (string actual, string expected) = await InCulture(culture, async () =>
        {
            var debts = new FakeDebtRepository();
            debts.SetThrowOnGetAll(new InvalidOperationException("boom"));
            DebtsViewModel vm = DebtsViewModelTests.CreateViewModel(debts, new FakeDebtPaymentRepository());
            await Task.Delay(1);
            return (vm.FormError, Strings.DebtsLoadFailed);
        });

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Recurring_SavingWithoutASelection_ResolvesThroughStrings(string culture)
    {
        (string actual, string expected) = await InCulture(culture, async () =>
        {
            RecurringRig rig = await NewRecurringRig();
            await rig.Vm.SaveCommand.ExecuteAsync(null);
            return (rig.Vm.FormError, Strings.RecurringSelectionRequired);
        });

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Recurring_CreateThenUpdate_ReportTheLocalizedStatuses(string culture)
    {
        (string created, string updated, string expectedCreated, string expectedUpdated) = await InCulture(culture, async () =>
        {
            RecurringRig rig = await NewRecurringRig();
            rig.Vm.Name = "Spotify";
            rig.Vm.Amount = 9m;
            rig.Vm.SelectedCategory = rig.Vm.Categories.Single();
            rig.Vm.SelectedPaymentMethod = rig.Vm.PaymentMethods.Single();
            await rig.Vm.SaveCommand.ExecuteAsync(null);
            string createdMessage = rig.Vm.StatusMessage;

            rig.Vm.EditCommand.Execute(rig.Vm.Rules.Single());
            await rig.Vm.SaveCommand.ExecuteAsync(null);
            return (createdMessage, rig.Vm.StatusMessage, Strings.RecurringCreated, Strings.RecurringUpdated);
        });

        Assert.Equal(expectedCreated, created);
        Assert.Equal(expectedUpdated, updated);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Recurring_DeactivateSkipDelete_ReportTheLocalizedStatuses(string culture)
    {
        (string[] actual, string[] expected) = await InCulture(culture, async () =>
        {
            RecurringRig rig = await NewRecurringRig(NewRule());
            var actualMessages = new List<string>();

            rig.Vm.SelectedRule = rig.Vm.Rules.Single();
            await rig.Vm.DeactivateSelectedCommand.ExecuteAsync(null);
            actualMessages.Add(rig.Vm.StatusMessage);
            await rig.Vm.SkipSelectedCommand.ExecuteAsync(null);
            actualMessages.Add(rig.Vm.StatusMessage);
            await rig.Vm.DeleteSelectedCommand.ExecuteAsync(null);
            actualMessages.Add(rig.Vm.StatusMessage);

            return (actualMessages.ToArray(), new[] { Strings.RecurringDeactivated, Strings.RecurringSkipped, Strings.RecurringDeleted });
        });

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Recurring_ConfirmGeneration_ReportsTheLocalizedCount(string culture)
    {
        (string actual, string expected) = await InCulture(culture, async () =>
        {
            RecurringRig rig = await NewRecurringRig(NewRule());
            await rig.Vm.ConfirmGenerationCommand.ExecuteAsync(null);
            return (rig.Vm.StatusMessage, string.Format(CultureInfo.CurrentUICulture, Strings.RecurringGenerated, 1));
        });

        Assert.Equal(expected, actual);
        Assert.Contains("1", actual, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Recurring_ALoadThatThrows_ReportsTheLocalizedLoadFailure(string culture)
    {
        (string actual, string expected) = await InCulture(culture, async () =>
        {
            RecurringRig rig = await NewRecurringRig(throwOnLoad: true);
            return (rig.Vm.FormError, Strings.RecurringLoadFailed);
        });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EveryNewMessage_DiffersBetweenCultures()
    {
        string[] names =
        [
            nameof(Strings.DebtsStartDateRequired), nameof(Strings.DebtsCreated), nameof(Strings.DebtsPaymentRegistered),
            nameof(Strings.DebtsSkipped), nameof(Strings.DebtsLoadFailed), nameof(Strings.DebtsScheduleLoadFailed),
            nameof(Strings.DebtsCreateFailed), nameof(Strings.DebtsPaymentFailed), nameof(Strings.DebtsSkipFailed),
            nameof(Strings.RecurringSelectionRequired), nameof(Strings.RecurringCreated), nameof(Strings.RecurringUpdated),
            nameof(Strings.RecurringDeactivated), nameof(Strings.RecurringSkipped), nameof(Strings.RecurringDeleted),
            nameof(Strings.RecurringLoadFailed), nameof(Strings.RecurringPreviewFailed), nameof(Strings.RecurringGenerateFailed),
            nameof(Strings.RecurringGenerated), nameof(Strings.RecurringSaveFailed), nameof(Strings.RecurringDeactivateFailed),
            nameof(Strings.RecurringSkipFailed), nameof(Strings.RecurringDeleteFailed),
        ];

        foreach (string name in names)
        {
            string es = Strings.ResourceManager.GetString(name, CultureInfo.GetCultureInfo("es-DO"))!;
            string en = Strings.ResourceManager.GetString(name, CultureInfo.GetCultureInfo("en"))!;
            Assert.False(string.IsNullOrWhiteSpace(es), name);
            Assert.NotEqual(es, en);
        }
    }

    [Theory]
    [InlineData("DebtsViewModel.cs")]
    [InlineData("RecurringRulesViewModel.cs")]
    public void TheViewModelSources_CarryNoSpanishSentenceLiteral(string file)
    {
        // Regression guard: a user-visible sentence must come from Strings, never from a string literal.
        var literal = new Regex("\"(?<text>[^\"\\\\]*(?:\\\\.[^\"\\\\]*)*)\"");
        var spanish = new Regex(
            "\\b(No se|Se generaron|correctamente|Selecciona|pudo|pudieron|Regla|Deuda|Cuota|Pago|Ocurrencia)\\b|[áéíóúñ¿¡]",
            RegexOptions.CultureInvariant);

        string[] hits =
        [
            .. File.ReadAllLines(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "ViewModels", file))
                .Select((line, index) => (line, number: index + 1))
                .Where(x => !x.line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Where(x => literal.Matches(x.line).Any(m => spanish.IsMatch(m.Groups["text"].Value)))
                .Select(x => $"{file}:{x.number} {x.line.Trim()}"),
        ];

        Assert.True(hits.Length == 0, "Spanish literals left in the view model:" + Environment.NewLine + string.Join(Environment.NewLine, hits));
    }
}
