using System.Globalization;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views.Charts;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// The dashboard charts speak the design palette (pre-1.0 slice E2): every category slice is painted with the
/// <see cref="ChartPalette"/> color of its index, the evolution lines use the income / expense tokens, and a legend
/// (label, color, amount, share) is exposed so the pie never relies on color alone.
/// </summary>
public sealed class DashboardChartStyleTests
{
    private static readonly DateOnly _today = new(2026, 3, 15);

    private static async Task<DashboardViewModel> LoadedAsync()
    {
        var food = Guid.NewGuid();
        var fun = Guid.NewGuid();
        FakeExpenseRepository expenses = new();
        expenses.CategoryNames[food] = "Comida";
        expenses.CategoryNames[fun] = "Ocio";
        await expenses.AddAsync(Expense.Create(food, _today, Money.Create(300m, Currency.DOP), "Super", null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        await expenses.AddAsync(Expense.Create(fun, _today, Money.Create(100m, Currency.DOP), "Cine", null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));

        DashboardViewModel vm = DashboardViewModelTests.CreateViewModel(expenses, new FakeIncomeRepository(), _today);
        await vm.RefreshCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task EachCategorySlice_IsPaintedWithThePaletteColorOfItsIndex()
    {
        DashboardViewModel vm = await LoadedAsync();

        Assert.Equal(2, vm.CategorySeries.Length);
        for (int i = 0; i < vm.CategorySeries.Length; i++)
        {
            PieSeries<double> pie = Assert.IsType<PieSeries<double>>(vm.CategorySeries[i]);
            SolidColorPaint fill = Assert.IsType<SolidColorPaint>(pie.Fill);
            Assert.Equal(ChartPalette.Slice(i), fill.Color);
        }
    }

    [Fact]
    public async Task TheLegend_HasOneEntryPerSlice_WithItsColorLabelAmountAndShare()
    {
        DashboardViewModel vm = await LoadedAsync();

        Assert.Equal(vm.CategorySlices.Count, vm.CategoryLegend.Count);
        CategoryLegendItem first = vm.CategoryLegend[0];
        Assert.Equal(vm.CategorySlices[0].Label, first.Label);
        Assert.Equal(ChartPalette.SliceHexes[0], first.ColorHex);
        Assert.Equal(ChartPalette.SliceHexes[1], vm.CategoryLegend[1].ColorHex);
        Assert.False(string.IsNullOrWhiteSpace(first.AmountDisplay));
        Assert.Equal(75m.ToString("0", CultureInfo.CurrentCulture) + "%", first.PercentDisplay);
        Assert.Equal(25m.ToString("0", CultureInfo.CurrentCulture) + "%", vm.CategoryLegend[1].PercentDisplay);
    }

    [Fact]
    public async Task TheLegend_ClearsWithTheSlices()
    {
        DashboardViewModel vm = await LoadedAsync();
        await vm.NextMonthCommand.ExecuteAsync(null);

        Assert.Empty(vm.CategorySlices);
        Assert.Empty(vm.CategoryLegend);
    }

    [Fact]
    public async Task TheEvolutionLines_UseTheIncomeAndExpenseTokens_AndTheAxesTheGridTokens()
    {
        DashboardViewModel vm = await LoadedAsync();

        LineSeries<double> expense = Assert.IsType<LineSeries<double>>(vm.EvolutionSeries[0]);
        LineSeries<double> income = Assert.IsType<LineSeries<double>>(vm.EvolutionSeries[1]);
        Assert.Equal(ChartPalette.Expense, Assert.IsType<SolidColorPaint>(expense.Stroke).Color);
        Assert.Equal(ChartPalette.Income, Assert.IsType<SolidColorPaint>(income.Stroke).Color);

        Axis x = Assert.Single(vm.EvolutionXAxes);
        Assert.Equal(ChartPalette.AxisText, Assert.IsType<SolidColorPaint>(x.LabelsPaint).Color);
        Axis y = Assert.Single(vm.EvolutionYAxes);
        Assert.Equal(ChartPalette.AxisText, Assert.IsType<SolidColorPaint>(y.LabelsPaint).Color);
        Assert.Equal(ChartPalette.Grid, Assert.IsType<SolidColorPaint>(y.SeparatorsPaint).Color);
        Assert.NotNull(y.Labeler);
        Assert.Equal(1.5.ToString("0.#", CultureInfo.CurrentCulture) + "k", y.Labeler!(1500));
    }
}
