using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ControlDeFinanzasIA.Tests.Unit.Packaging;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.Tests.Unit.Localization;

/// <summary>
/// F-021 S5a (R-19, S-38): generic parity guard over the REAL resx files and the REAL Designer. It extends, and does
/// not replace, the per-feature completeness tests (for example <c>ErrorOverlayResourcesTests</c>).
/// </summary>
public sealed partial class StringsParityTests
{
    /// <summary>
    /// Pre-existing keys that are read through <c>IStringLocalizer</c> by name and so never had a Designer accessor.
    /// Kept as an explicit list (visible debt) instead of silently skipping the accessor check for them.
    /// </summary>
    private static readonly string[] _keysWithoutAccessorByDesign =
    [
        "ExpensesAiSuggestionPrefix",
        "ExpensesAiSuggestionAcceptHint",
        "ExpensesAiSuggestionDismissHint",
        "ExpensesAiNotConfiguredMessage",
        "ExpensesAiUnavailableMessage",
        "ExpensesAiSuggestionLoading",
    ];

    private static Dictionary<string, string> Load(string file) =>
        XDocument.Load(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Resources", file))
            .Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);

    [GeneratedRegex(@"\{\d+(?:[:,][^}]*)?\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\{x:Static\s+resources:Strings\.(\w+)\s*\}")]
    private static partial Regex StaticStringReference();

    [Fact]
    public void EveryNeutralKey_ExistsInSpanish_AndViceVersa()
    {
        HashSet<string> neutral = [.. Load("Strings.resx").Keys];
        HashSet<string> spanish = [.. Load("Strings.es-DO.resx").Keys];

        Assert.Empty(neutral.Except(spanish));
        Assert.Empty(spanish.Except(neutral));
    }

    [Fact]
    public void NoValueIsEmpty_InEitherFile()
    {
        foreach (string file in new[] { "Strings.resx", "Strings.es-DO.resx" })
        {
            string[] empty = [.. Load(file).Where(p => string.IsNullOrWhiteSpace(p.Value)).Select(p => p.Key)];
            Assert.True(empty.Length == 0, $"{file} has empty values: {string.Join(", ", empty)}");
        }
    }

    [Fact]
    public void ThePlaceholderCount_MatchesBetweenNeutralAndSpanish_ForEveryKey()
    {
        Dictionary<string, string> neutral = Load("Strings.resx");
        Dictionary<string, string> spanish = Load("Strings.es-DO.resx");

        string[] mismatched =
        [
            .. neutral.Keys
                .Where(spanish.ContainsKey)
                .Where(k => Placeholder().Matches(neutral[k]).Count != Placeholder().Matches(spanish[k]).Count),
        ];

        Assert.True(mismatched.Length == 0, $"Placeholder count differs for: {string.Join(", ", mismatched)}");
    }

    [Fact]
    public void EveryStaticStringReferencedFromAView_HasADesignerProperty_AndAResxKey()
    {
        string uiRoot = RepoRoot.Combine("src", "ControlDeFinanzasIA.UI");
        Dictionary<string, string> neutral = Load("Strings.resx");

        string[] views =
        [
            .. Directory.EnumerateFiles(Path.Combine(uiRoot, "Views"), "*.axaml", SearchOption.AllDirectories),
            .. Directory.Exists(Path.Combine(uiRoot, "Controls"))
                ? Directory.EnumerateFiles(Path.Combine(uiRoot, "Controls"), "*.axaml", SearchOption.AllDirectories)
                : [],
        ];

        Assert.NotEmpty(views);

        var problems = new List<string>();
        foreach (string file in views)
        {
            foreach (Match match in StaticStringReference().Matches(File.ReadAllText(file)))
            {
                string key = match.Groups[1].Value;
                PropertyInfo? property = typeof(Strings).GetProperty(key, BindingFlags.Public | BindingFlags.Static);
                if (property is null)
                {
                    problems.Add($"{Path.GetFileName(file)}: no Designer property '{key}'");
                }
                else if (!neutral.ContainsKey(key))
                {
                    problems.Add($"{Path.GetFileName(file)}: '{key}' is not in Strings.resx");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void EveryResxKey_HasADesignerProperty_ExceptTheExplicitAllowlist()
    {
        string[] missing =
        [
            .. Load("Strings.resx").Keys
                .Where(k => typeof(Strings).GetProperty(k, BindingFlags.Public | BindingFlags.Static) is null)
                .Except(_keysWithoutAccessorByDesign),
        ];

        Assert.True(missing.Length == 0, $"Keys without a Designer accessor: {string.Join(", ", missing)}");
    }

    [Fact]
    public void TheAllowlist_ContainsOnlyKeysThatStillLackAnAccessor()
    {
        // A stale entry would silently hide a future regression: drop it the moment an accessor exists.
        string[] stale =
        [
            .. _keysWithoutAccessorByDesign
                .Where(k => typeof(Strings).GetProperty(k, BindingFlags.Public | BindingFlags.Static) is not null),
        ];

        Assert.True(stale.Length == 0, $"Allowlist entries that now have an accessor: {string.Join(", ", stale)}");
    }
}
