using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.UI.Converters;
using ControlDeFinanzasIA.UI.ViewModels;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Dark mode: the REAL application resources and styles in the headless session resolve their tokens per theme variant,
/// controls that already exist are re-skinned when the variant changes (no restart), the stock Fluent controls follow the
/// variant, and the converters that return a fixed brush read the variant that is active NOW.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class ThemeVariantHeadlessTests
{
    private static readonly Color _lightSurface = Color.Parse("#FFFFFF");
    private static readonly Color _darkSurface = Color.Parse("#1D1C17");
    private static readonly Color _lightAccent = Color.Parse("#3459E6");
    private static readonly Color _darkAccent = Color.Parse("#7088F0");
    private static readonly Color _lightText = Color.Parse("#14192B");
    private static readonly Color _darkText = Color.Parse("#F3F0E8");

    private static ContentPresenter Presenter(Control control) =>
        control.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");

    private static Border NamedBorder(Control control, string name) =>
        control.GetVisualDescendants().OfType<Border>().First(b => b.Name == name);

    private static Color? ColorOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task TheTokens_ResolveToTheActiveVariant_OnRealStyledControls(string name)
    {
        ThemeVariant variant = name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        (Color? card, Color? primary, Color? title) = await HeadlessUi.OnUiThread(() =>
        {
            Avalonia.Application app = Avalonia.Application.Current!;
            ThemeVariant? original = app.RequestedThemeVariant;
            app.RequestedThemeVariant = variant;
            var cardBorder = new Border { Classes = { "card" }, Child = new TextBlock { Text = "Card" } };
            var button = new Button { Content = "Save", Classes = { "ledger-btn-primary" } };
            var heading = new TextBlock { Text = "Title", Classes = { "page-title" } };
            Window w = HeadlessUi.Show(new StackPanel { Children = { cardBorder, button, heading } }, null!);
            try
            {
                return Task.FromResult((ColorOf(cardBorder.Background), ColorOf(Presenter(button).Background), ColorOf(heading.Foreground)));
            }
            finally
            {
                w.Close();
                app.RequestedThemeVariant = original;
            }
        });

        Assert.Equal(name == "Dark" ? _darkSurface : _lightSurface, card);
        Assert.Equal(name == "Dark" ? _darkAccent : _lightAccent, primary);
        Assert.Equal(name == "Dark" ? _darkText : _lightText, title);
    }

    [Fact]
    public async Task ControlsThatAlreadyExist_AreReSkinned_WhenTheVariantChangesLive()
    {
        (Color? lightCard, Color? darkCard, Color? backCard, Color? lightFill, Color? darkFill, Color? lightLabel, Color? darkLabel) =
            await HeadlessUi.OnUiThread(() =>
            {
                Avalonia.Application app = Avalonia.Application.Current!;
                ThemeVariant? original = app.RequestedThemeVariant;
                app.RequestedThemeVariant = ThemeVariant.Light;
                var card = new Border { Classes = { "card" }, Child = new TextBlock { Text = "x" } };
                var button = new Button { Content = "Save", Classes = { "ledger-btn-primary" } };
                var heading = new TextBlock { Text = "Title", Classes = { "page-title" } };
                Window w = HeadlessUi.Show(new StackPanel { Children = { card, button, heading } }, null!);
                try
                {
                    Color? a = ColorOf(card.Background);
                    Color? fillA = ColorOf(Presenter(button).Background);
                    Color? labelA = ColorOf(heading.Foreground);

                    app.RequestedThemeVariant = ThemeVariant.Dark;
                    HeadlessUi.Flush();
                    Color? b = ColorOf(card.Background);
                    Color? fillB = ColorOf(Presenter(button).Background);
                    Color? labelB = ColorOf(heading.Foreground);

                    app.RequestedThemeVariant = ThemeVariant.Light;
                    HeadlessUi.Flush();
                    Color? c = ColorOf(card.Background);
                    return Task.FromResult((a, b, c, fillA, fillB, labelA, labelB));
                }
                finally
                {
                    w.Close();
                    app.RequestedThemeVariant = original;
                }
            });

        Assert.Equal(_lightSurface, lightCard);
        Assert.Equal(_darkSurface, darkCard);
        Assert.Equal(_lightSurface, backCard);
        Assert.Equal(_lightAccent, lightFill);
        Assert.Equal(_darkAccent, darkFill);
        Assert.Equal(_lightText, lightLabel);
        Assert.Equal(_darkText, darkLabel);
    }

    [Fact]
    public async Task StockFluentControls_FollowTheVariant_TextBoxAndComboBoxUseTheSurfaceToken()
    {
        (Color? lightBox, Color? darkBox, Color? lightCombo, Color? darkCombo, Color? lightWindow, Color? darkWindow) =
            await HeadlessUi.OnUiThread(() =>
            {
                Avalonia.Application app = Avalonia.Application.Current!;
                ThemeVariant? original = app.RequestedThemeVariant;
                var box = new TextBox { Width = 200, Text = "hello" };
                var combo = new ComboBox { Width = 200, ItemsSource = new[] { "A", "B" }, SelectedIndex = 0 };
                var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { box, combo } } };
                app.RequestedThemeVariant = ThemeVariant.Light;
                window.Show();
                HeadlessUi.Flush();
                try
                {
                    Color? boxLight = ColorOf(NamedBorder(box, "PART_BorderElement").Background);
                    Color? comboLight = ColorOf(NamedBorder(combo, "Background").Background);
                    Color? windowLight = ColorOf(window.Background);

                    app.RequestedThemeVariant = ThemeVariant.Dark;
                    HeadlessUi.Flush();
                    Color? boxDark = ColorOf(NamedBorder(box, "PART_BorderElement").Background);
                    Color? comboDark = ColorOf(NamedBorder(combo, "Background").Background);
                    Color? windowDark = ColorOf(window.Background);
                    return Task.FromResult((boxLight, boxDark, comboLight, comboDark, windowLight, windowDark));
                }
                finally
                {
                    window.Close();
                    app.RequestedThemeVariant = original;
                }
            });

        Assert.Equal(_lightSurface, lightBox);
        Assert.Equal(_darkSurface, darkBox);
        Assert.Equal(_lightSurface, lightCombo);
        Assert.Equal(_darkSurface, darkCombo);
        Assert.NotEqual(lightWindow, darkWindow);
    }

    [Fact]
    public async Task TheBrushConverters_ReadTheActiveVariant_NotTheDefaultOne()
    {
        (Color?[] light, Color?[] dark) = await HeadlessUi.OnUiThread(() =>
        {
            Avalonia.Application app = Avalonia.Application.Current!;
            ThemeVariant? original = app.RequestedThemeVariant;
            try
            {
                app.RequestedThemeVariant = ThemeVariant.Light;
                HeadlessUi.Flush();
                Color?[] l = Sample();
                app.RequestedThemeVariant = ThemeVariant.Dark;
                HeadlessUi.Flush();
                Color?[] d = Sample();
                return Task.FromResult((l, d));
            }
            finally
            {
                app.RequestedThemeVariant = original;
            }
        });

        Assert.Equal(
            new Color?[] { _lightAccent, Color.Parse("#E8EDFD"), Color.Parse("#B82A44"), Color.Parse("#5B6272"), Color.Parse("#B82A44") },
            light);
        Assert.Equal(
            new Color?[] { _darkAccent, Color.Parse("#1A213F"), Color.Parse("#F27B93"), Color.Parse("#A7A398"), Color.Parse("#F27B93") },
            dark);
    }

    private static Color?[] Sample()
    {
        System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
        return
        [
            ColorOf(StatusToneConverter.Instance.Convert(DebtStatus.Active, typeof(IBrush), "fg", c) as IBrush),
            ColorOf(StatusToneConverter.Instance.Convert(DebtStatus.Active, typeof(IBrush), "bg", c) as IBrush),
            ColorOf(BudgetStatusBrushConverter.Instance.Convert(BudgetStatus.Exceeded, typeof(IBrush), null, c) as IBrush),
            ColorOf(ChatMessageStateBrushConverter.Instance.Convert(ChatMessageState.Pending, typeof(IBrush), null, c) as IBrush),
            ColorOf(AnomalySeverityBrushConverter.Instance.Convert(AnomalySeverity.Critical, typeof(IBrush), "text", c) as IBrush),
        ];
    }
}
