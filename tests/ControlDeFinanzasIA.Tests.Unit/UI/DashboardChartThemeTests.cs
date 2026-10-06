using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views.Charts;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Dark mode: the dashboard charts paint with Skia colours, not resources, so the view-model picks the palette of the
/// ACTIVE theme when it builds the series, the axes, the legend and the slice gaps, and rebuilds them (without a reload)
/// when the theme changes live.
/// </summary>
public sealed class DashboardChartThemeTests
{
    private static readonly DateOnly _today = new(2026, 3, 15);

    private static async Task<DashboardViewModel> LoadedAsync(FakeThemeService theme)
    {
        var food = Guid.NewGuid();
        var fun = Guid.NewGuid();
        FakeExpenseRepository expenses = new();
        expenses.CategoryNames[food] = "Comida";
        expenses.CategoryNames[fun] = "Ocio";
        await expenses.AddAsync(Expense.Create(food, _today, Money.Create(300m, Currency.DOP), "Super", null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        await expenses.AddAsync(Expense.Create(fun, _today, Money.Create(100m, Currency.DOP), "Cine", null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));

        DashboardViewModel vm = DashboardViewModelTests.CreateViewModel(expenses, new FakeIncomeRepository(), _today, themeService: theme);
        await vm.RefreshCommand.ExecuteAsync(null);
        return vm;
    }

    private static void AssertPalette(DashboardViewModel vm, ChartColors expected)
    {
        for (int i = 0; i < vm.CategorySeries.Length; i++)
        {
            PieSeries<double> pie = Assert.IsType<PieSeries<double>>(vm.CategorySeries[i]);
            Assert.Equal(expected.Slice(i), Assert.IsType<SolidColorPaint>(pie.Fill).Color);
            Assert.Equal(expected.Surface, Assert.IsType<SolidColorPaint>(pie.Stroke).Color);
            Assert.Equal(expected.SliceHex(i), vm.CategoryLegend[i].ColorHex);
        }

        LineSeries<double> expense = Assert.IsType<LineSeries<double>>(vm.EvolutionSeries[0]);
        LineSeries<double> income = Assert.IsType<LineSeries<double>>(vm.EvolutionSeries[1]);
        Assert.Equal(expected.Expense, Assert.IsType<SolidColorPaint>(expense.Stroke).Color);
        Assert.Equal(expected.Income, Assert.IsType<SolidColorPaint>(income.Stroke).Color);
        Assert.Equal(expected.Surface, Assert.IsType<SolidColorPaint>(income.GeometryFill).Color);
        Assert.Equal(expected.AxisText, Assert.IsType<SolidColorPaint>(Assert.Single(vm.EvolutionXAxes).LabelsPaint).Color);
        Axis y = Assert.Single(vm.EvolutionYAxes);
        Assert.Equal(expected.AxisText, Assert.IsType<SolidColorPaint>(y.LabelsPaint).Color);
        Assert.Equal(expected.Grid, Assert.IsType<SolidColorPaint>(y.SeparatorsPaint).Color);
    }

    [Fact]
    public async Task WhenTheThemeIsDark_TheChartsAreBuiltWithTheDarkPalette()
    {
        var theme = new FakeThemeService();
        theme.Apply(AppTheme.Dark);

        DashboardViewModel vm = await LoadedAsync(theme);

        AssertPalette(vm, ChartPalette.For(AppTheme.Dark));
        Assert.NotEqual(SKColors.White, ChartPalette.For(AppTheme.Dark).Surface);
    }

    [Fact]
    public async Task WhenTheThemeIsLight_TheChartsKeepTheLightPalette()
    {
        var theme = new FakeThemeService();

        DashboardViewModel vm = await LoadedAsync(theme);

        AssertPalette(vm, ChartPalette.For(AppTheme.Light));
    }

    [Fact]
    public async Task WhenTheThemeChangesLive_TheLoadedChartsAreRebuiltWithoutAReload()
    {
        var theme = new FakeThemeService();
        DashboardViewModel vm = await LoadedAsync(theme);
        int slices = vm.CategorySlices.Count;

        theme.Apply(AppTheme.Dark);

        AssertPalette(vm, ChartPalette.For(AppTheme.Dark));
        Assert.Equal(slices, vm.CategorySlices.Count);
        Assert.Equal(2, vm.CategorySeries.Length);

        theme.Apply(AppTheme.Light);

        AssertPalette(vm, ChartPalette.For(AppTheme.Light));
    }

    [Fact]
    public async Task WhenNoChartIsLoaded_ChangingTheThemeDoesNotInventAny()
    {
        var theme = new FakeThemeService();
        DashboardViewModel vm = DashboardViewModelTests.CreateViewModel(new FakeExpenseRepository(), new FakeIncomeRepository(), _today, themeService: theme);
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.NextMonthCommand.ExecuteAsync(null);

        Exception? failure = Record.Exception(() => theme.Apply(AppTheme.Dark));

        Assert.Null(failure);
        Assert.Empty(vm.CategorySeries);
        Assert.Empty(vm.CategoryLegend);
    }

    [Fact]
    public async Task TheSubscription_IsWeak_SoANavigatedAwayDashboardCanBeCollected()
    {
        var theme = new FakeThemeService();
        WeakReference reference = await CreateAndDropAsync(theme);

        for (int i = 0; i < 5 && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(20);
        }

        Assert.False(reference.IsAlive, "The theme service must not keep a dashboard view-model alive.");
    }

    private static async Task<WeakReference> CreateAndDropAsync(FakeThemeService theme)
    {
        DashboardViewModel vm = await LoadedAsync(theme);
        return new WeakReference(vm);
    }
}
