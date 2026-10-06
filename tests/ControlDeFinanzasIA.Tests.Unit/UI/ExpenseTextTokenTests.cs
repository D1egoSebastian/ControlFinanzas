using System.Xml.Linq;
using ControlDeFinanzasIA.Tests.Unit.Packaging;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// The semantic token set of the "Sand and Cobalt" palette (pre-1.0 slice E). Replaces the F-021 Terracotta checks: the
/// expense colour is now a single text-safe token (<c>Expense</c>, measured in <c>ContrastTests</c>) used for both text
/// and fills, and the legacy Libro Mayor names are gone. Reads the REAL token and style files.
/// </summary>
public sealed class ExpenseTextTokenTests
{
    private static readonly XNamespace _x = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string Styles(string file) =>
        File.ReadAllText(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Styles", file));

    [Theory]
    [InlineData("Canvas")]
    [InlineData("Surface")]
    [InlineData("SurfaceMuted")]
    [InlineData("Divider")]
    [InlineData("InputBorder")]
    [InlineData("Text")]
    [InlineData("TextSecondary")]
    [InlineData("TextMuted")]
    [InlineData("Accent")]
    [InlineData("AccentHover")]
    [InlineData("AccentSoft")]
    [InlineData("OnAccent")]
    [InlineData("FocusRing")]
    [InlineData("Income")]
    [InlineData("IncomeSoft")]
    [InlineData("Expense")]
    [InlineData("ExpenseSoft")]
    [InlineData("Warning")]
    [InlineData("WarningFill")]
    [InlineData("WarningSoft")]
    public void TheTokenFiles_DefineEachSemanticColor_AndItsBrush_InBothVariants(string name)
    {
        foreach (string variant in new[] { "Light", "Dark" })
        {
            XElement root = XDocument.Load(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Styles", $"Tokens.{variant}.axaml")).Root!;

            XElement? color = root.Elements().SingleOrDefault(e => (string?)e.Attribute(_x + "Key") == name + "Color");
            XElement? brush = root.Elements().SingleOrDefault(e => (string?)e.Attribute(_x + "Key") == name + "Brush");

            Assert.NotNull(color);
            Assert.NotNull(brush);
            Assert.Contains(name + "Color", (string?)brush!.Attribute("Color"), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("InkColor")]
    [InlineData("PaperColor")]
    [InlineData("AmberColor")]
    [InlineData("SageColor")]
    [InlineData("TerracottaColor")]
    [InlineData("HairlineColor")]
    public void TheLegacyLibroMayorPalette_IsGone(string legacyKey)
    {
        foreach (string file in new[] { "Tokens.axaml", "Tokens.Light.axaml", "Tokens.Dark.axaml" })
        {
            XElement root = XDocument.Load(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Styles", file)).Root!;

            Assert.DoesNotContain(root.Elements(), e => (string?)e.Attribute(_x + "Key") == legacyKey);
        }
    }

    [Fact]
    public void TheExportErrorStatusStyle_UsesTheExpenseToken()
    {
        string styles = Styles("LedgerStyles.axaml");
        int start = styles.IndexOf("TextBlock.ledger-status.export-error", StringComparison.Ordinal);

        Assert.True(start >= 0);
        string block = styles[start..styles.IndexOf("</Style>", start, StringComparison.Ordinal)];
        Assert.Contains("ExpenseBrush", block, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("RadiusSmall")]
    [InlineData("RadiusMedium")]
    [InlineData("RadiusLarge")]
    [InlineData("RadiusPill")]
    public void TheRadiusScale_IsDefinedInTheTokens(string key)
    {
        XElement root = XDocument.Load(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Styles", "Tokens.axaml")).Root!;

        Assert.Contains(root.Elements(), e => (string?)e.Attribute(_x + "Key") == key);
    }
}
