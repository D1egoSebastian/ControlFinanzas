using System.Globalization;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.ViewModels;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Month and weekday names follow the app's UI language (<see cref="CultureInfo.CurrentUICulture"/>), never the OS
/// regional format (<see cref="CultureInfo.CurrentCulture"/>). Every test puts the two cultures at odds on purpose.
/// </summary>
public sealed class UiCultureMonthNamesTests
{
    private static readonly DateOnly _october = new(2026, 10, 14);

    [Theory]
    [InlineData("es-DO", "en-US", "octubre")]
    [InlineData("en", "es-DO", "October")]
    public async Task DashboardMonthLabel_AxisAndCountLine_FollowTheUiCulture(string ui, string regional, string monthName)
    {
        using CultureScope _ = new(ui, regional);
        FakeExpenseRepository expenses = new() { PageAddedItems = true };
        var food = Guid.NewGuid();
        expenses.CategoryNames[food] = "Comida";
        FakeCategoryRepository categories = new();
        categories.SetCategories([new ControlDeFinanzasIA.Domain.Catalog.Category { Id = food, Name = "Comida" }]);
        await expenses.AddAsync(Expense.Create(
            food, _october, Money.Create(100m, Currency.DOP), "Test", null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));

        DashboardViewModel vm = DashboardViewModelTests.CreateViewModel(expenses, new FakeIncomeRepository(), _october, categoryRepo: categories);
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Contains(monthName, vm.SelectedMonthLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(monthName, vm.MonthExpensesCountText, StringComparison.OrdinalIgnoreCase);
        Axis axis = Assert.Single(vm.EvolutionXAxes);
        string last = axis.Labels![^1];
        Assert.StartsWith(monthName[..3], last, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CultureInfo.GetCultureInfo(ui).DateTimeFormat.GetAbbreviatedMonthName(10).TrimEnd('.'), last.TrimEnd('.'), ignoreCase: true);
    }

    [Theory]
    [InlineData("es-DO", "en-US", "octubre")]
    [InlineData("en", "es-DO", "October")]
    public void BudgetsMonthLabel_FollowsTheUiCulture(string ui, string regional, string monthName)
    {
        using CultureScope _ = new(ui, regional);
        BudgetsViewModel vm = BudgetsViewModelTests.CreateViewModel(clock: new FakeClock { Today = _october });

        Assert.Contains(monthName, vm.SelectedMonthLabel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CalendarMonthLabel_WeekdayHeadersAndCellNames_FollowTheUiCulture()
    {
        using CultureScope _ = new("es-DO", "en-US");
        FakeClock clock = new() { Today = _october, UtcNow = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc) };
        CalendarViewModel vm = new(
            new ControlDeFinanzasIA.Application.UseCases.Calendar.GetCalendarMonth.GetCalendarMonthQuery(new FakeExpenseRepository(), new FakeCurrencyConverter(), clock),
            new FakeNavigationService(),
            clock,
            NullLogger<CalendarViewModel>.Instance);

        Assert.Contains("octubre", vm.SelectedMonthLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(7, vm.WeekdayHeaders.Count);
        string[] spanish = CultureInfo.GetCultureInfo("es-DO").DateTimeFormat.AbbreviatedDayNames;
        Assert.All(vm.WeekdayHeaders, h => Assert.Contains(h, spanish));
        Assert.Contains("octubre", vm.Cells.First(c => c.IsCurrentMonth).AutomationName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CultureInfo.GetCultureInfo("es-DO").DateTimeFormat.FirstDayOfWeek, FirstWeekday(vm, spanish));
    }

    private static DayOfWeek FirstWeekday(CalendarViewModel vm, string[] names) => (DayOfWeek)Array.IndexOf(names, vm.WeekdayHeaders[0]);

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _ui = CultureInfo.CurrentUICulture;
        private readonly CultureInfo _regional = CultureInfo.CurrentCulture;

        public CultureScope(string ui, string regional)
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(ui);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(regional);
        }

        public void Dispose()
        {
            CultureInfo.CurrentUICulture = _ui;
            CultureInfo.CurrentCulture = _regional;
        }
    }
}
