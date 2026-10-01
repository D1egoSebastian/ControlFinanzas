using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.UI.Converters;
using ControlDeFinanzasIA.UI.Navigation;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Pre-1.0 slice E (E1): the design system resolves on REAL controls carrying the REAL application styles in the
/// headless session. Buttons are pills, inputs and cards use the radius scale of the tokens, the sidebar's current item
/// is a cobalt-tinted pill, every sidebar route has a vector icon, and the app renders the light theme.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class DesignSystemStyleTests
{
    private static readonly Color _accent = Color.Parse("#3459E6");
    private static readonly Color _accentSoft = Color.Parse("#E8EDFD");
    private static readonly Color _surfaceMuted = Color.Parse("#EFEDE6");

    private static T Resource<T>(string key)
    {
        Assert.True(Avalonia.Application.Current!.TryGetResource(key, ThemeVariant.Default, out object? value), $"Resource {key} is missing.");
        return Assert.IsType<T>(value);
    }

    private static Task<T> ResourceAsync<T>(string key) => HeadlessUi.OnUiThread(() => Task.FromResult(Resource<T>(key)));

    private static ContentPresenter Presenter(Control control) =>
        control.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");

    [Fact]
    public async Task TheApplication_RendersTheLightTheme()
    {
        ThemeVariant variant = await HeadlessUi.OnUiThread(() => Task.FromResult(Avalonia.Application.Current!.RequestedThemeVariant!));

        Assert.Equal(ThemeVariant.Light, variant);
    }

    [Fact]
    public async Task ThePrimaryButton_IsAPill_FilledWithTheAccent()
    {
        (CornerRadius radius, Color? fill) = await HeadlessUi.OnUiThread(() =>
        {
            var button = new Button { Content = "Save", Classes = { "ledger-btn-primary" } };
            Window window = HeadlessUi.Show(new StackPanel { Children = { button } }, null!);
            try
            {
                ContentPresenter presenter = Presenter(button);
                return Task.FromResult((presenter.CornerRadius, (presenter.Background as ISolidColorBrush)?.Color));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(await ResourceAsync<CornerRadius>("RadiusPill"), radius);
        Assert.Equal(_accent, fill);
    }

    [Fact]
    public async Task TheSecondaryAndGhostButtons_ArePills_WithTheirOwnFill()
    {
        (CornerRadius secondaryRadius, Color? secondaryFill, CornerRadius ghostRadius, Color? ghostFill) = await HeadlessUi.OnUiThread(() =>
        {
            var secondary = new Button { Content = "Edit", Classes = { "ledger-btn" } };
            var ghost = new Button { Content = "Edit", Classes = { "ledger-btn", "ghost" } };
            Window window = HeadlessUi.Show(new StackPanel { Children = { secondary, ghost } }, null!);
            try
            {
                ContentPresenter a = Presenter(secondary);
                ContentPresenter b = Presenter(ghost);
                return Task.FromResult((a.CornerRadius, (a.Background as ISolidColorBrush)?.Color, b.CornerRadius, (b.Background as ISolidColorBrush)?.Color));
            }
            finally
            {
                window.Close();
            }
        });

        CornerRadius pill = await ResourceAsync<CornerRadius>("RadiusPill");
        Assert.Equal(pill, secondaryRadius);
        Assert.Equal(pill, ghostRadius);
        Assert.Equal(_surfaceMuted, secondaryFill);
        Assert.Equal(Colors.Transparent, ghostFill);
    }

    [Fact]
    public async Task TheTextBox_AndTheComboBox_UseTheMediumRadius()
    {
        (CornerRadius text, CornerRadius combo) = await HeadlessUi.OnUiThread(() =>
        {
            var box = new TextBox { Width = 200 };
            var select = new ComboBox { Width = 200, ItemsSource = new[] { "A" } };
            Window window = HeadlessUi.Show(new StackPanel { Children = { box, select } }, null!);
            try
            {
                return Task.FromResult((box.CornerRadius, select.CornerRadius));
            }
            finally
            {
                window.Close();
            }
        });

        CornerRadius medium = await ResourceAsync<CornerRadius>("RadiusMedium");
        Assert.Equal(medium, text);
        Assert.Equal(medium, combo);
    }

    [Fact]
    public async Task TheCard_IsRoundedAndElevated()
    {
        (CornerRadius radius, int shadows) = await HeadlessUi.OnUiThread(() =>
        {
            var card = new Border { Classes = { "card" }, Child = new TextBlock { Text = "Card" } };
            Window window = HeadlessUi.Show(new StackPanel { Children = { card } }, null!);
            try
            {
                return Task.FromResult((card.CornerRadius, card.BoxShadow.Count));
            }
            finally
            {
                window.Close();
            }
        });

        CornerRadius large = await ResourceAsync<CornerRadius>("RadiusLarge");
        Assert.Equal(large, radius);
        Assert.True(radius.TopLeft >= 12 && radius.TopLeft <= 16);
        Assert.True(shadows >= 1, "Cards carry a soft elevation instead of a hard rule.");
    }

    [Fact]
    public async Task TheCurrentSidebarItem_IsACobaltTintedPill_AndTheOthersAreTransparent()
    {
        (Color? current, Color? other, CornerRadius radius) = await HeadlessUi.OnUiThread(() =>
        {
            var selected = new Button { Content = "Dashboard", Classes = { "sidebar-item", "current" } };
            var plain = new Button { Content = "Expenses", Classes = { "sidebar-item" } };
            Window window = HeadlessUi.Show(new StackPanel { Width = 200, Children = { selected, plain } }, null!);
            try
            {
                ContentPresenter a = Presenter(selected);
                ContentPresenter b = Presenter(plain);
                return Task.FromResult(((a.Background as ISolidColorBrush)?.Color, (b.Background as ISolidColorBrush)?.Color, a.CornerRadius));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(_accentSoft, current);
        Assert.Equal(Colors.Transparent, other);
        Assert.True(radius.TopLeft >= 8, "The selected state is a rounded pill, not a hard rule.");
    }

    [Fact]
    public async Task EverySidebarRoute_HasAVectorIcon()
    {
        string[] missing = await HeadlessUi.OnUiThread(() =>
            Task.FromResult(NavigationSections.All
                .Where(s => RouteIconConverter.Instance.Convert(s.Route, typeof(Geometry), null, System.Globalization.CultureInfo.InvariantCulture) is not Geometry)
                .Select(s => s.Route)
                .ToArray()));

        Assert.Empty(missing);
    }
}
