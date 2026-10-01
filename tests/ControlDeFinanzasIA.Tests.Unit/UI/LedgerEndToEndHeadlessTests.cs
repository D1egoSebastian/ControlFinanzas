using Avalonia.Controls;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.Accessibility;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using static ControlDeFinanzasIA.Tests.Unit.UI.LedgerViewKit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// End to end over the REAL composition root, the REAL window and a migrated temp SQLite file: 45 expenses and 45
/// incomes are seeded straight through the repositories, then the lists are driven through the screens the way a user
/// does (sidebar shortcut, Next, filter, Delete + Confirm) and the database is read back to prove the effect is
/// persisted. This is the test that would have caught the Phase 6 view rewrite dropping the pager and the delete.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class LedgerEndToEndHeadlessTests
{
    private static readonly DateTime _stamp = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static async Task SeedAsync(RealShellRig rig, int count)
    {
        using IServiceScope scope = rig.Services.CreateScope();
        IReadOnlyList<Category> categories = await scope.ServiceProvider.GetRequiredService<ICategoryRepository>().GetAllAsync(default);
        Guid category = categories[0].Id;
        IExpenseRepository expenses = scope.ServiceProvider.GetRequiredService<IExpenseRepository>();
        IIncomeRepository incomes = scope.ServiceProvider.GetRequiredService<IIncomeRepository>();
        for (int i = 0; i < count; i++)
        {
            DateOnly date = new DateOnly(2026, 1, 1).AddDays(i);
            Assert.True((await expenses.AddAsync(Expense.Create(category, date, Money.Create(10m + i, Currency.DOP), $"Gasto {i}", null, _stamp.AddMinutes(i)))).IsSuccess);
            Assert.True((await incomes.AddAsync(Income.Create(category, date, Money.Create(100m + i, Currency.DOP), $"Ingreso {i}", null, null, _stamp.AddMinutes(i)))).IsSuccess);
        }
    }

    private static async Task<int> PersistedExpensesAsync(RealShellRig rig)
    {
        using IServiceScope scope = rig.Services.CreateScope();
        PagedResult<Expense> page = await scope.ServiceProvider.GetRequiredService<IExpenseRepository>().GetByFilterAsync(new ExpenseFilter { PageSize = 1 });
        return page.Total;
    }

    private static async Task<int> PersistedIncomesAsync(RealShellRig rig)
    {
        using IServiceScope scope = rig.Services.CreateScope();
        PagedResult<Income> page = await scope.ServiceProvider.GetRequiredService<IIncomeRepository>().GetByFilterAsync(new IncomeFilter { PageSize = 1 });
        return page.Total;
    }

    [Fact]
    public async Task Expenses_ThroughTheRealShell_PageFilterAndDeleteWithConfirmationPersist()
    {
        (int FirstRows, int LastPageRows, bool OldVisible, int PersistedBefore, int PersistedAfter, int TotalAfter, int Filtered) result = await OnUi(async () =>
        {
            await using RealShellRig rig = await RealShellRig.CreateAsync("en");
            await SeedAsync(rig, 45);
            await rig.OpenSectionAsync(1);
            ExpensesViewModel vm = Assert.IsType<ExpensesViewModel>(rig.Shell.Current);
            await rig.SettleAsync(() => vm.HasLoaded && vm.TotalCount == 45);
            ExpensesView view = rig.Window.GetVisualDescendants().OfType<ExpensesView>().Single();

            ExpenseDto[] Rows() =>
                [.. view.GetVisualDescendants().OfType<Button>()
                    .Where(b => ReferenceEquals(b.Command, vm.BeginEditCommand) && b.IsEffectivelyVisible)
                    .Select(b => (ExpenseDto)b.CommandParameter!)];

            int firstRows = Rows().Length;
            Press(rig.Window, ButtonFor(view, vm.NextPageCommand));
            await rig.SettleAsync(() => vm.Page == 2);
            Press(rig.Window, ButtonFor(view, vm.NextPageCommand));
            await rig.SettleAsync(() => vm.Page == 3);
            ExpenseDto[] last = Rows();
            ExpenseDto old = last.Single(r => r.Description == "Gasto 0");

            int before = await PersistedExpensesAsync(rig);
            Press(rig.Window, ButtonFor(view, vm.DeleteExpenseCommand, old));
            Press(rig.Window, ButtonFor(view, vm.DeleteExpenseCommand, old));
            await rig.SettleAsync(() => vm.TotalCount == 44);
            int after = await PersistedExpensesAsync(rig);

            TextBox search = view.GetVisualDescendants().OfType<TextBox>().Single(t => Avalonia.Automation.AutomationProperties.GetName(t) == "Search");
            search.Text = "Gasto 4";
            Press(rig.Window, ButtonFor(view, vm.ApplyFilterCommand));
            await rig.SettleAsync(() => vm.TotalCount == 6);

            return (firstRows, last.Length, Rows().Any(r => r.Description == "Gasto 0"), before, after, vm.TotalCount, Rows().Length);
        });

        Assert.Equal(20, result.FirstRows);
        Assert.Equal(5, result.LastPageRows);
        Assert.False(result.OldVisible);
        Assert.Equal(45, result.PersistedBefore);
        Assert.Equal(44, result.PersistedAfter);
        Assert.Equal(6, result.TotalAfter);
        Assert.Equal(6, result.Filtered);
    }

    [Fact]
    public async Task Income_ThroughTheRealShell_PageFilterAndDeleteWithConfirmationPersist()
    {
        (int FirstRows, bool OldVisible, int PersistedBefore, int PersistedAfter, int Filtered) result = await OnUi(async () =>
        {
            await using RealShellRig rig = await RealShellRig.CreateAsync("en");
            await SeedAsync(rig, 45);
            await rig.OpenSectionAsync(2);
            IncomeViewModel vm = Assert.IsType<IncomeViewModel>(rig.Shell.Current);
            await rig.SettleAsync(() => vm.HasLoaded && vm.TotalCount == 45);
            IncomeView view = rig.Window.GetVisualDescendants().OfType<IncomeView>().Single();

            IncomeDto[] Rows() =>
                [.. view.GetVisualDescendants().OfType<Button>()
                    .Where(b => ReferenceEquals(b.Command, vm.BeginEditCommand) && b.IsEffectivelyVisible)
                    .Select(b => (IncomeDto)b.CommandParameter!)];

            int firstRows = Rows().Length;
            Press(rig.Window, ButtonFor(view, vm.NextPageCommand));
            await rig.SettleAsync(() => vm.Page == 2);
            Press(rig.Window, ButtonFor(view, vm.NextPageCommand));
            await rig.SettleAsync(() => vm.Page == 3);
            IncomeDto old = Rows().Single(r => r.Description == "Ingreso 0");

            int before = await PersistedIncomesAsync(rig);
            Press(rig.Window, ButtonFor(view, vm.DeleteIncomeCommand, old));
            Press(rig.Window, ButtonFor(view, vm.DeleteIncomeCommand, old));
            await rig.SettleAsync(() => vm.TotalCount == 44);
            int after = await PersistedIncomesAsync(rig);

            TextBox search = view.GetVisualDescendants().OfType<TextBox>().Single(t => Avalonia.Automation.AutomationProperties.GetName(t) == "Search");
            search.Text = "Ingreso 4";
            Press(rig.Window, ButtonFor(view, vm.ApplyFilterCommand));
            await rig.SettleAsync(() => vm.TotalCount == 6);

            return (firstRows, Rows().Any(r => r.Description == "Ingreso 0"), before, after, Rows().Length);
        });

        Assert.Equal(20, result.FirstRows);
        Assert.False(result.OldVisible);
        Assert.Equal(45, result.PersistedBefore);
        Assert.Equal(44, result.PersistedAfter);
        Assert.Equal(6, result.Filtered);
    }
}
