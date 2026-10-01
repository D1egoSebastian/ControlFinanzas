using System.Globalization;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for the Dashboard's "expenses of the month" list (the real <see cref="DashboardViewModel"/>
/// over the real <c>GetMonthExpensesQuery</c>; only repositories, clock and navigation are doubles).
/// </summary>
public sealed class DashboardMonthExpensesViewModelTests
{
    private static readonly Guid _foodId = Guid.NewGuid();
    private static readonly DateOnly _today = new(2026, 3, 15);

    private sealed record Rig(
        DashboardViewModel ViewModel,
        FakeExpenseRepository Expenses,
        FakeNavigationService Navigation,
        ExpensesViewModel ExpensesViewModel);

    private static Rig Create(Action<FakeExpenseRepository>? seed = null, FakeCurrencyConverter? converter = null)
    {
        FakeExpenseRepository expenses = new() { PageAddedItems = true };
        expenses.CategoryNames[_foodId] = "Comida";
        seed?.Invoke(expenses);

        FakeCategoryRepository categories = new();
        categories.SetCategories([new Category { Id = _foodId, Name = "Comida" }]);

        ExpensesViewModel expensesViewModel = ExpensesViewModelTests.CreateViewModel();
        FakeNavigationService navigation = new(new Dictionary<string, ViewModelBase>(StringComparer.Ordinal)
        {
            { RouteNames.Expenses, expensesViewModel },
        });

        DashboardViewModel viewModel = DashboardViewModelTests.CreateViewModel(
            expenses,
            new FakeIncomeRepository(),
            _today,
            converter: converter,
            categoryRepo: categories,
            navigation: navigation);

        return new Rig(viewModel, expenses, navigation, expensesViewModel);
    }

    private static void Add(FakeExpenseRepository repo, DateOnly date, decimal amount, string description, Currency currency = Currency.DOP, int minute = 0)
        => repo.AddAsync(Expense.Create(
            _foodId,
            date,
            Money.Create(amount, currency),
            description,
            null,
            new DateTime(2026, 1, 1, 12, minute, 0, DateTimeKind.Utc))).GetAwaiter().GetResult();

    [Fact]
    public async Task Load_ListsTheSelectedMonthsExpenses_MostRecentFirst_WithAllTheRowFields()
    {
        Rig rig = Create(e =>
        {
            Add(e, new DateOnly(2026, 3, 2), 100m, "Supermercado");
            Add(e, new DateOnly(2026, 3, 12), 1234.5m, "Gasolina");
            Add(e, new DateOnly(2026, 2, 27), 999m, "Del mes pasado");
        });

        await rig.ViewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Gasolina", "Supermercado" }, rig.ViewModel.MonthExpenses.Select(r => r.Description).ToArray());
        MonthExpenseRow first = rig.ViewModel.MonthExpenses[0];
        Assert.Equal("Comida", first.CategoryName);
        Assert.Equal(new DateOnly(2026, 3, 12).ToString("d MMM", CultureInfo.CurrentCulture), first.DateDisplay);
        Assert.Equal($"{1234.5m:N2} DOP", first.AmountDisplay);
        Assert.Equal(string.Empty, first.DopDisplay);
        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, Strings.DashboardMonthExpensesCountFormat, 2, rig.ViewModel.SelectedMonthLabel),
            rig.ViewModel.MonthExpensesCountText);
        Assert.True(rig.ViewModel.HasMonthExpenses);
        Assert.False(rig.ViewModel.IsMonthExpensesEmpty);
    }

    [Fact]
    public async Task Load_ASingleExpense_UsesTheSingularCount()
    {
        Rig rig = Create(e => Add(e, new DateOnly(2026, 3, 2), 100m, "Uno"));

        await rig.ViewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, Strings.DashboardMonthExpensesCountOneFormat, rig.ViewModel.SelectedMonthLabel),
            rig.ViewModel.MonthExpensesCountText);
    }

    [Fact]
    public async Task Load_MoreThanTheCap_ListsTheLatestEightAndTheFullCount()
    {
        Rig rig = Create(e =>
        {
            for (int day = 1; day <= 11; day++)
            {
                Add(e, new DateOnly(2026, 3, day), day, $"Gasto {day}");
            }
        });

        await rig.ViewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(DashboardDefaults.MonthExpensesLimit, rig.ViewModel.MonthExpenses.Count);
        Assert.Equal("Gasto 11", rig.ViewModel.MonthExpenses[0].Description);
        Assert.Equal("Gasto 4", rig.ViewModel.MonthExpenses[^1].Description);
        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, Strings.DashboardMonthExpensesCountFormat, 11, rig.ViewModel.SelectedMonthLabel),
            rig.ViewModel.MonthExpensesCountText);
        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, Strings.DashboardMonthExpensesShowingFormat, 8),
            rig.ViewModel.MonthExpensesShowingText);
    }

    [Fact]
    public async Task Load_AtOrUnderTheCap_HasNoShowingLatestNote()
    {
        Rig rig = Create(e =>
        {
            for (int day = 1; day <= 8; day++)
            {
                Add(e, new DateOnly(2026, 3, day), day, $"Gasto {day}");
            }
        });

        await rig.ViewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(8, rig.ViewModel.MonthExpenses.Count);
        Assert.Equal(string.Empty, rig.ViewModel.MonthExpensesShowingText);
    }

    [Fact]
    public async Task MonthNavigation_ChangesTheList_AndTheCountFollowsTheMonthLabel()
    {
        Rig rig = Create(e =>
        {
            Add(e, new DateOnly(2026, 3, 2), 100m, "Marzo");
            Add(e, new DateOnly(2026, 2, 20), 50m, "Febrero A");
            Add(e, new DateOnly(2026, 2, 5), 60m, "Febrero B");
        });
        await rig.ViewModel.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "Marzo" }, rig.ViewModel.MonthExpenses.Select(r => r.Description).ToArray());

        await rig.ViewModel.PreviousMonthCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Febrero A", "Febrero B" }, rig.ViewModel.MonthExpenses.Select(r => r.Description).ToArray());
        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, Strings.DashboardMonthExpensesCountFormat, 2, rig.ViewModel.SelectedMonthLabel),
            rig.ViewModel.MonthExpensesCountText);

        await rig.ViewModel.NextMonthCommand.ExecuteAsync(null);
        await rig.ViewModel.NextMonthCommand.ExecuteAsync(null);

        Assert.Empty(rig.ViewModel.MonthExpenses);
    }

    [Fact]
    public async Task EmptyMonth_FlagsTheEmptyState_AndAMonthWithExpensesDoesNot()
    {
        Rig rig = Create(e => Add(e, new DateOnly(2026, 2, 5), 60m, "Solo febrero"));

        await rig.ViewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(rig.ViewModel.MonthExpenses);
        Assert.True(rig.ViewModel.IsMonthExpensesEmpty);
        Assert.False(rig.ViewModel.HasMonthExpenses);

        await rig.ViewModel.PreviousMonthCommand.ExecuteAsync(null);

        Assert.Single(rig.ViewModel.MonthExpenses);
        Assert.False(rig.ViewModel.IsMonthExpensesEmpty);
        Assert.True(rig.ViewModel.HasMonthExpenses);
    }

    [Fact]
    public async Task ForeignCurrencyRows_ShowTheDopEquivalent_AndAMissingRateLeavesItBlank()
    {
        FakeCurrencyConverter converter = new((source, target) =>
            source.Amount == 10m
                ? Result.Success(Money.Create(source.Amount * 60m, target))
                : Result.Failure("ExchangeRateUnavailable", "no rate"));
        Rig rig = Create(
            e =>
            {
                Add(e, new DateOnly(2026, 3, 9), 10m, "Con tasa", Currency.USD);
                Add(e, new DateOnly(2026, 3, 8), 7m, "Sin tasa", Currency.USD);
            },
            converter);

        await rig.ViewModel.RefreshCommand.ExecuteAsync(null);

        MonthExpenseRow withRate = rig.ViewModel.MonthExpenses[0];
        MonthExpenseRow withoutRate = rig.ViewModel.MonthExpenses[1];
        Assert.Equal($"{10m:N2} USD", withRate.AmountDisplay);
        Assert.Contains($"{600m:N2} DOP", withRate.DopDisplay, StringComparison.Ordinal);
        Assert.Equal(string.Empty, withoutRate.DopDisplay);
        Assert.True(rig.ViewModel.HasMissingRates);
    }

    [Fact]
    public async Task ViewAll_NavigatesToExpensesFilteredToTheDisplayedMonth_AndFollowsMonthNavigation()
    {
        Rig rig = Create(e => Add(e, new DateOnly(2026, 3, 2), 100m, "Marzo"));
        await rig.ViewModel.RefreshCommand.ExecuteAsync(null);

        rig.ViewModel.ViewAllExpensesCommand.Execute(null);

        Assert.Equal(new[] { RouteNames.Expenses }, rig.Navigation.NavigateToTypedCalls.ToArray());
        Assert.Equal(new DateOnly(2026, 3, 1), rig.ExpensesViewModel.CurrentFilter.DateFrom);
        Assert.Equal(new DateOnly(2026, 3, 31), rig.ExpensesViewModel.CurrentFilter.DateTo);

        await rig.ViewModel.PreviousMonthCommand.ExecuteAsync(null);
        rig.ViewModel.ViewAllExpensesCommand.Execute(null);

        Assert.Equal(2, rig.Navigation.NavigateToTypedCalls.Count);
        Assert.Equal(new DateOnly(2026, 2, 1), rig.ExpensesViewModel.CurrentFilter.DateFrom);
        Assert.Equal(new DateOnly(2026, 2, 28), rig.ExpensesViewModel.CurrentFilter.DateTo);
    }

    [Fact]
    public async Task ALoadFailure_ShowsTheLocalizedError_KeepsTheRestOfTheDashboard_AndDoesNotClaimTheMonthIsEmpty()
    {
        Rig rig = Create(e => Add(e, new DateOnly(2026, 3, 2), 100m, "Marzo"));
        rig.Expenses.SetThrowOnGetFilter(new InvalidOperationException("db down"));

        await rig.ViewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(Strings.DashboardLoadErrorMessage, rig.ViewModel.LoadError);
        Assert.Empty(rig.ViewModel.MonthExpenses);
        Assert.False(rig.ViewModel.IsMonthExpensesEmpty);
        Assert.False(rig.ViewModel.HasMonthExpenses);
        Assert.Equal(string.Empty, rig.ViewModel.MonthExpensesCountText);
        Assert.False(string.IsNullOrEmpty(rig.ViewModel.TotalExpensesDisplay)); // the summary still loaded
    }
}
