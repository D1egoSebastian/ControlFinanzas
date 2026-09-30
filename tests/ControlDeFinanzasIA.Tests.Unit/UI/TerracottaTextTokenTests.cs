using System.Xml.Linq;
using ControlDeFinanzasIA.Tests.Unit.Packaging;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S5a (R-18 prerequisite, Reconciliation #15): <c>Terracotta #D96459</c> on <c>Surface</c> measures 4.43:1, so
/// error TEXT uses a dedicated lighter token while <c>Terracotta</c> stays for fills and large amounts. Reads the
/// REAL token and style files.
/// </summary>
public sealed class TerracottaTextTokenTests
{
    private static readonly XNamespace _x = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string Styles(string file) =>
        File.ReadAllText(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Styles", file));

    [Fact]
    public void TheTokenFile_DefinesTerracottaTextColor_AndItsBrush_WithoutChangingTerracotta()
    {
        XElement root = XDocument.Load(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Styles", "Tokens.axaml")).Root!;

        string? text = root.Elements().SingleOrDefault(e => (string?)e.Attribute(_x + "Key") == "TerracottaTextColor")?.Value;
        string? fill = root.Elements().SingleOrDefault(e => (string?)e.Attribute(_x + "Key") == "TerracottaColor")?.Value;
        XElement? brush = root.Elements().SingleOrDefault(e => (string?)e.Attribute(_x + "Key") == "TerracottaTextBrush");

        Assert.Equal("#F08A82", text);
        Assert.Equal("#D96459", fill);
        Assert.NotNull(brush);
        Assert.Contains("TerracottaTextColor", (string?)brush!.Attribute("Color"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheExportErrorStatusStyle_UsesTheTextToken()
    {
        string styles = Styles("LedgerStyles.axaml");
        int start = styles.IndexOf("TextBlock.ledger-status.export-error", StringComparison.Ordinal);

        Assert.True(start >= 0);
        string block = styles[start..styles.IndexOf("</Style>", start, StringComparison.Ordinal)];
        Assert.Contains("TerracottaTextBrush", block, StringComparison.Ordinal);
        Assert.DoesNotContain("{StaticResource TerracottaBrush}", block, StringComparison.Ordinal);
    }
}
