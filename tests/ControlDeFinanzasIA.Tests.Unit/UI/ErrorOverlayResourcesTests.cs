using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ControlDeFinanzasIA.Tests.Unit.Packaging;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S4 (R-10, R-19 prerequisite): the overlay's resx keys exist in BOTH languages with a Designer accessor, and
/// the overlay view keeps its accessibility contract and its token-only styling. Reads the REAL resx and axaml files.
/// </summary>
public sealed class ErrorOverlayResourcesTests
{
    private static readonly string[] _keys =
    [
        "ErrorOverlayTitle",
        "ErrorOverlayBody",
        "ErrorOverlayRestartHint",
        "ErrorOverlayIdLabel",
        "ErrorOverlayMore",
        "ErrorOverlayCopy",
        "ErrorOverlayCopied",
        "ErrorOverlayClose",
    ];

    private static Dictionary<string, string> Load(string file) =>
        XDocument.Load(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Resources", file))
            .Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);

    [Fact]
    public void EveryOverlayKey_ExistsInBothResxFiles_NonEmpty_WithAMatchingPlaceholderCount()
    {
        Dictionary<string, string> neutral = Load("Strings.resx");
        Dictionary<string, string> spanish = Load("Strings.es-DO.resx");

        foreach (string key in _keys)
        {
            Assert.True(neutral.TryGetValue(key, out string? en), $"Strings.resx lacks {key}");
            Assert.True(spanish.TryGetValue(key, out string? es), $"Strings.es-DO.resx lacks {key}");
            Assert.False(string.IsNullOrWhiteSpace(en), key);
            Assert.False(string.IsNullOrWhiteSpace(es), key);
            Assert.Equal(Regex.Matches(en!, @"\{\d+\}").Count, Regex.Matches(es!, @"\{\d+\}").Count);
        }

        Assert.Contains("{0}", neutral["ErrorOverlayMore"], StringComparison.Ordinal);
        Assert.Contains("{0}", spanish["ErrorOverlayMore"], StringComparison.Ordinal);
    }

    [Fact]
    public void EveryOverlayKey_HasADesignerAccessor_ThatFollowsTheCulture()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            foreach (string key in _keys)
            {
                PropertyInfo? property = typeof(Strings).GetProperty(key, BindingFlags.Public | BindingFlags.Static);
                Assert.NotNull(property);
            }

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-DO");
            string spanish = Strings.ErrorOverlayCopy;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            string english = Strings.ErrorOverlayCopy;

            Assert.Equal("Copiar detalles", spanish);
            Assert.Equal("Copy details", english);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void TheOverlayView_KeepsItsAccessibilityContract_AndUsesTokensOnly()
    {
        string xaml = File.ReadAllText(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Views", "ErrorOverlayView.axaml"));

        Assert.Contains("KeyboardNavigation.TabNavigation=\"Cycle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{x:Static resources:Strings.ErrorOverlayTitle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Gesture=\"Escape\"", xaml, StringComparison.Ordinal);
        Assert.Contains("behaviors:FocusOnRequest.Token=\"{Binding InitialFocus}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex("=\"#[0-9A-Fa-f]{3,8}\""), xaml);

        // Every visible string comes from the resources, never a literal.
        foreach (Match m in Regex.Matches(xaml, "\\b(?:Text|Content)=\"([^\"]*)\""))
        {
            Assert.StartsWith("{", m.Groups[1].Value, StringComparison.Ordinal);
        }
    }
}
