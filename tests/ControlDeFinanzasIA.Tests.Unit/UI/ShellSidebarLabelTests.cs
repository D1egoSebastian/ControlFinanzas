using System.Globalization;
using ControlDeFinanzasIA.Tests.Unit.Packaging;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S6 (R-15, R-19, S-32, S-38): the sidebar labels of the REAL <see cref="ShellViewModel"/> come from the
/// REAL resx files through the REAL <see cref="IStringLocalizer{T}"/> registered by <c>AddLocalization()</c>, exactly
/// as the composition root does. Only the navigation service and notification center around the shell are doubles.
/// </summary>
public sealed class ShellSidebarLabelTests
{
    private static readonly string[] _routes =
    [
        RouteNames.Dashboard,
        RouteNames.Expenses,
        RouteNames.Incomes,
        RouteNames.Recurring,
        RouteNames.Debts,
        RouteNames.Calendar,
        RouteNames.Budgets,
        RouteNames.Goals,
        RouteNames.Chat,
        RouteNames.Settings,
    ];

    private static Dictionary<string, string> LabelsIn(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            using ServiceProvider provider = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
            var shell = new ShellViewModel(
                new FakeNavigationService(),
                provider.GetRequiredService<IStringLocalizer<Strings>>(),
                new NotificationListViewModel(
                    new FakeNotificationCenter(),
                    new FakeClock { Today = new DateOnly(2026, 9, 20) },
                    NullLogger<NotificationListViewModel>.Instance));

            return shell.MenuItems.ToDictionary(item => item.Route, item => item.DisplayName);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void AllTenSidebarRoutes_ArePresent_InBothCultures()
    {
        Assert.Equal(_routes, LabelsIn("en").Keys);
        Assert.Equal(_routes, LabelsIn("es-DO").Keys);
    }

    [Theory]
    [InlineData("es-DO", "Recurrentes", "Deudas", "Calendario")]
    [InlineData("en", "Recurring", "Debts", "Calendar")]
    public void TheThreeFormerlyHardcodedLabels_FollowTheActiveCulture(string culture, string recurring, string debts, string calendar)
    {
        Dictionary<string, string> labels = LabelsIn(culture);

        Assert.Equal(recurring, labels[RouteNames.Recurring]);
        Assert.Equal(debts, labels[RouteNames.Debts]);
        Assert.Equal(calendar, labels[RouteNames.Calendar]);
    }

    [Fact]
    public void EveryLabel_IsALocalizedValue_NeverAKeyFallback_AndResolvesFromResx()
    {
        foreach (string culture in new[] { "es-DO", "en" })
        {
            Dictionary<string, string> labels = LabelsIn(culture);
            foreach ((string route, string label) in labels)
            {
                Assert.False(string.IsNullOrWhiteSpace(label), route);
                Assert.DoesNotContain("Sidebar", label, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void TheDifferentLabels_DifferBetweenCultures()
    {
        Dictionary<string, string> es = LabelsIn("es-DO");
        Dictionary<string, string> en = LabelsIn("en");

        foreach (string route in new[] { RouteNames.Recurring, RouteNames.Debts, RouteNames.Calendar })
        {
            Assert.NotEqual(es[route], en[route]);
        }
    }

    [Fact]
    public void TheShellSource_HasNoStringLiteralAsASidebarLabel()
    {
        string[] hits =
        [
            .. File.ReadAllLines(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "ViewModels", "ShellViewModel.cs"))
                .Select((line, index) => (line, number: index + 1))
                .Where(x => x.line.Contains("new RouteMenuItem(", StringComparison.Ordinal))
                .Where(x => !x.line.Contains("_localizer[\"", StringComparison.Ordinal))
                .Select(x => $"ShellViewModel.cs:{x.number} {x.line.Trim()}"),
        ];

        Assert.True(hits.Length == 0, "Sidebar labels that bypass the localizer:" + Environment.NewLine + string.Join(Environment.NewLine, hits));
    }
}
