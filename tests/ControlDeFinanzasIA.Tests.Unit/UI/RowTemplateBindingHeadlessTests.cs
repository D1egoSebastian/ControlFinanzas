using Avalonia.Controls;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateSubscription;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeactivateRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeleteRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.GenerateDueRecurring;
using ControlDeFinanzasIA.Application.UseCases.Recurring.SkipNextOccurrence;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateSubscription;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Headless Avalonia regression tests for the buttons inside list-row templates (Phase 20 verification, pre-existing
/// defect). A row template has the ROW as its data context, so a row button reaches the screen's view-model through
/// the view's <c>Root</c> element. The form <c>#Root.((vm:XViewModel)DataContext).Command</c> is the cast pattern that
/// crashed <c>ExpensesView</c>; each test renders the REAL view over the REAL view-model with a real data-bound row
/// and proves that the row's buttons are wired to the view-model's command instances and that the command runs.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class RowTemplateBindingHeadlessTests
{
    private static Task<T> OnUiThread<T>(Func<Task<T>> body) => HeadlessUi.OnUiThread(body);

    private static void Flush() => HeadlessUi.Flush();

    // The row buttons are the ones whose CommandParameter is the row itself.
    private static List<Button> RowButtons(Control root, object row) =>
        root.GetVisualDescendants().OfType<Button>().Where(b => ReferenceEquals(b.CommandParameter, row)).ToList();

    private static Button RowButtonFor(Control root, object row, System.Windows.Input.ICommand command)
    {
        List<Button> buttons = RowButtons(root, row);
        Assert.NotEmpty(buttons);
        return Assert.Single(buttons, b => ReferenceEquals(b.Command, command));
    }

    [Fact]
    public async Task BudgetsView_RowButtons_ResolveTheViewModelCommands_AndEditRuns()
    {
        bool ok = await OnUiThread(async () =>
        {
            BudgetsViewModel vm = BudgetsViewModelTests.CreateViewModel();
            var row = new BudgetRow(Guid.NewGuid(), Guid.NewGuid(), "Comida", 100m, Currency.DOP);
            vm.Budgets.Add(row);
            var view = new BudgetsView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            Flush();

            Button edit = RowButtonFor(view, row, vm.EditCommand);
            RowButtonFor(view, row, vm.DeleteCommand);

            edit.Command!.Execute(edit.CommandParameter);
            Assert.Same(row, vm.SelectedBudget);
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task ChatView_MessageRow_CopyButtonResolvesTheViewModelCommand_AndCopiesTheBubbleText()
    {
        bool ok = await OnUiThread(async () =>
        {
            ChatViewModelTests.Sut sut = ChatViewModelTests.CreateSut();
            var message = ChatMessageItem.User("Hola, cuanto gaste?");
            sut.ViewModel.Messages.Add(message);
            var view = new ChatView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, sut.ViewModel));
            Flush();

            Button copy = RowButtonFor(view, message, sut.ViewModel.CopyCommand);
            Assert.True(copy.Command!.CanExecute(copy.CommandParameter));

            await sut.ViewModel.CopyCommand.ExecuteAsync(message);
            Assert.Equal("Hola, cuanto gaste?", sut.Clipboard.LastText);
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task ChatView_AssistantRows_InEveryState_RenderWithAWorkingCopyButton()
    {
        bool ok = await OnUiThread(async () =>
        {
            ChatViewModelTests.Sut sut = ChatViewModelTests.CreateSut();
            var answered = ChatMessageItem.Pending("...");
            answered.Complete("Gastaste 10.", ChatMessageState.Answered);
            var unavailable = ChatMessageItem.Pending("...");
            unavailable.Complete("No disponible.", ChatMessageState.Unavailable);
            var pending = ChatMessageItem.Pending("Pensando");
            foreach (ChatMessageItem item in new[] { answered, unavailable, pending })
            {
                sut.ViewModel.Messages.Add(item);
            }

            var view = new ChatView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, sut.ViewModel));
            Flush();

            foreach (ChatMessageItem item in new[] { answered, unavailable, pending })
            {
                Button copy = RowButtonFor(view, item, sut.ViewModel.CopyCommand);
                Assert.Equal(item.State != ChatMessageState.Pending, copy.IsEnabled);
            }

            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task DashboardView_AnomalyRow_DismissButtonResolvesTheViewModelCommand()
    {
        bool ok = await OnUiThread(async () =>
        {
            DashboardViewModel vm = DashboardViewModelTests.CreateViewModel(
                new FakeExpenseRepository(),
                new FakeIncomeRepository(),
                new DateOnly(2026, 9, 20));
            var row = new AnomalyListItem(Guid.NewGuid(), "Comida", "Cena cara", "RD$ 9,000.00", "15 sep 2026", AnomalySeverity.Warning, "Media", "Fuera de lo normal.");
            vm.Anomalies.Add(row);
            var view = new DashboardView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            Flush();

            RowButtonFor(view, row, vm.DismissAnomalyCommand);
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task DebtsView_ScheduleRow_PayAndSkipButtonsResolveTheViewModelCommands()
    {
        bool ok = await OnUiThread(async () =>
        {
            DebtsViewModel vm = DebtsViewModelTests.CreateViewModel(new FakeDebtRepository(), new FakeDebtPaymentRepository());
            var debt = Debt.Create(
                "Prestamo",
                Money.Create(1500m, Currency.DOP),
                Currency.DOP,
                totalInstallments: 1,
                installmentAmount: Money.Create(1500m, Currency.DOP),
                frequency: nameof(DebtFrequency.Monthly),
                startDate: new DateOnly(2026, 9, 1));
            DebtPayment payment = new DebtScheduleGenerator().Generate(debt)[0];
            vm.Schedule.Add(payment);
            var view = new DebtsView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            Flush();

            RowButtonFor(view, payment, vm.RegisterPaymentCommand);
            RowButtonFor(view, payment, vm.SkipPaymentCommand);
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task GoalsView_GoalRow_PlanSelectAndDeleteButtonsResolveTheViewModelCommands_AndSelectRuns()
    {
        bool ok = await OnUiThread(async () =>
        {
            GoalsViewModel vm = GoalsViewModelTests.CreateViewModel();
            var row = new GoalRow(
                Guid.NewGuid(), "Viaje", Currency.DOP, 1000m, 100m, 900m, 10m, new DateOnly(2027, 1, 1), GoalStatus.Active, 6, 150m, false, false);
            vm.Goals.Add(row);
            var view = new GoalsView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            Flush();

            RowButtonFor(view, row, vm.RequestPlanCommand);
            Button select = RowButtonFor(view, row, vm.SelectCommand);
            RowButtonFor(view, row, vm.DeleteCommand);

            select.Command!.Execute(select.CommandParameter);
            Assert.Same(row, vm.SelectedGoal);
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task IncomeView_IncomeRow_EditButtonResolvesTheViewModelCommand_AndEnterEditMode()
    {
        bool ok = await OnUiThread(async () =>
        {
            IncomeViewModel vm = IncomeViewModelTests.CreateViewModel();
            var dto = new IncomeDto(
                Guid.NewGuid(), null, new DateOnly(2026, 1, 10), 500m, Currency.DOP, "Sueldo", null, null,
                new DateTime(2026, 1, 10, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 10, 10, 0, 0, DateTimeKind.Utc), null, null, null);
            vm.Incomes.Add(dto);
            var view = new IncomeView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            Flush();

            Button edit = RowButtonFor(view, dto, vm.BeginEditCommand);

            edit.Command!.Execute(edit.CommandParameter);
            Assert.True(vm.IsEditMode);
            Assert.Equal(dto.Id, vm.EditableId);
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task RecurringRulesView_RuleRow_EditAndSkipButtonsResolveTheViewModelCommands_AndEditRuns()
    {
        bool ok = await OnUiThread(async () =>
        {
            RecurringRulesViewModel vm = NewRecurringRulesViewModel();
            RecurringRule rule = RecurringRule.Create(
                "Netflix", Money.Create(15m, Currency.DOP), Currency.DOP, "Monthly", new DateOnly(2026, 1, 1), null, Guid.NewGuid(), Guid.NewGuid());
            vm.Rules.Add(rule);
            var view = new RecurringRulesView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            Flush();

            Button edit = RowButtonFor(view, rule, vm.EditCommand);
            edit.Command!.Execute(edit.CommandParameter);
            Assert.Same(rule, vm.SelectedRule);

            // "Saltar" carries its own row: it can never skip another rule than the one it sits in.
            RowButtonFor(view, rule, vm.SkipCommand);
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    internal static RecurringRulesViewModel NewRecurringRulesViewModel(FakeRecurringRuleRepository? seeded = null, bool withCatalog = false)
    {
        FakeRecurringRuleRepository rules = seeded ?? new FakeRecurringRuleRepository();
        var clock = new FakeClock { Today = new DateOnly(2026, 9, 20), UtcNow = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc) };
        var categories = new FakeCategoryRepository();
        var methods = new FakePaymentMethodRepository();
        if (withCatalog)
        {
            categories.SetCategories([new ControlDeFinanzasIA.Domain.Catalog.Category { Id = new Guid("11111111-1111-1111-1111-000000000001"), Name = "Servicios", IsSystem = false }]);
            methods.Methods = [new ControlDeFinanzasIA.Domain.Catalog.PaymentMethod { Id = new Guid("22222222-2222-2222-2222-000000000001"), Name = "Tarjeta", IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }];
        }

        return new RecurringRulesViewModel(
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
            clock,
            NullLogger<RecurringRulesViewModel>.Instance);
    }
}
