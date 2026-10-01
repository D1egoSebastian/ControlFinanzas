using System.Text.RegularExpressions;
using ControlDeFinanzasIA.Tests.Unit.Packaging;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 verify W3 (R-19): no view or control hardcodes a hex color; colors come from <c>Styles/Tokens.axaml</c>. The
/// only exceptions are listed, with the reason, in <see cref="_allowlist"/>.
/// </summary>
public sealed class InlineColorScanTests
{
    private static readonly Regex _hex = new(@"#[0-9A-Fa-f]{8}\b|#[0-9A-Fa-f]{6}\b|#[0-9A-Fa-f]{3,4}\b", RegexOptions.Compiled);

    /// <summary>file (relative to the UI project) -> literal that is accepted, with the reason.</summary>
    /// <remarks>Empty since slice E: the one former exception (the popup drop shadow) is now the ShadowPopup BoxShadows token.</remarks>
    private static readonly (string File, string Literal, string Reason)[] _allowlist = [];

    [Fact]
    public void TheScan_CoversTheViewsAndTheControls()
    {
        string[] files = ScannedFiles();

        Assert.Contains(Path.Combine("Views", "MainWindow.axaml"), files);
        Assert.Contains(Path.Combine("Views", "SettingsView.axaml"), files);
        Assert.Contains(Path.Combine("Controls", "EmptyState.axaml"), files);
        Assert.True(files.Length >= 16, $"Only {files.Length} view files found.");
    }

    [Fact]
    public void NoViewOrControl_HardcodesAHexColor_OutsideTheJustifiedAllowlist()
    {
        var hits = new List<string>();
        foreach (string relative in ScannedFiles())
        {
            string[] lines = File.ReadAllLines(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", relative));
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (Match m in _hex.Matches(lines[i]))
                {
                    if (!_allowlist.Any(a => a.File == relative && a.Literal == m.Value))
                    {
                        hits.Add($"{relative}:{i + 1}: {m.Value}");
                    }
                }
            }
        }

        Assert.True(hits.Count == 0, "Inline hex colors (use a token from Styles/Tokens.axaml):" + Environment.NewLine + string.Join(Environment.NewLine, hits));
    }

    [Fact]
    public void EveryAllowlistEntry_IsStillNeededAndJustified()
    {
        foreach ((string file, string literal, string reason) in _allowlist)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason));
            string text = File.ReadAllText(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", file));
            Assert.Contains(literal, text, StringComparison.Ordinal);
        }
    }

    private static string[] ScannedFiles()
    {
        string root = RepoRoot.Combine("src", "ControlDeFinanzasIA.UI");
        return
        [
            .. new[] { "Views", "Controls" }
                .SelectMany(d => Directory.EnumerateFiles(Path.Combine(root, d), "*.axaml", SearchOption.AllDirectories))
                .Select(f => Path.GetRelativePath(root, f))
                .Order(StringComparer.Ordinal),
        ];
    }
}
