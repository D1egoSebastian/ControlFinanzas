using System.Xml.Linq;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Packaging;

/// <summary>F-021 S9: the shared publish properties are RID-conditioned and wired into the UI project (R-21, R-25, DD-12).</summary>
public sealed class PublishPropsTests
{
    private const string _ridCondition = "'$(RuntimeIdentifier)' != ''";

    private static readonly Dictionary<string, string> _expected = new()
    {
        ["SelfContained"] = "true",
        ["PublishSingleFile"] = "false",
        ["PublishTrimmed"] = "false",
        ["PublishReadyToRun"] = "false",
        ["DebugType"] = "embedded",
        ["SatelliteResourceLanguages"] = "en;es;es-DO",
        ["InvariantGlobalization"] = "false",
    };

    private static XElement[] PropertyElements()
    {
        var doc = XDocument.Load(RepoRoot.Combine("build", "Publish.props"));
        return doc.Descendants()
            .Where(e => e.Parent is { Name.LocalName: "PropertyGroup" })
            .ToArray();
    }

    private static bool IsRidConditioned(XElement property) =>
        Normalize(property.Attribute("Condition")?.Value) == _ridCondition
        || Normalize(property.Parent!.Attribute("Condition")?.Value) == _ridCondition;

    private static string Normalize(string? value) => (value ?? string.Empty).Trim();

    [Fact]
    public void EveryRequiredPropertyExistsWithItsValue()
    {
        XElement[] props = PropertyElements();

        foreach ((string name, string value) in _expected)
        {
            XElement element = Assert.Single(props, p => p.Name.LocalName == name);
            Assert.Equal(value, element.Value.Trim());
        }
    }

    [Fact]
    public void EveryPropertyIsConditionedOnARuntimeIdentifier()
    {
        XElement[] props = PropertyElements();

        Assert.NotEmpty(props);
        Assert.All(props, p => Assert.True(IsRidConditioned(p), p.Name.LocalName + " must be RID-conditioned"));
    }

    [Fact]
    public void UiProjectImportsThePublishProps()
    {
        var doc = XDocument.Load(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "ControlDeFinanzasIA.UI.csproj"));

        string[] imports = doc.Descendants()
            .Where(e => e.Name.LocalName == "Import")
            .Select(e => (e.Attribute("Project")?.Value ?? string.Empty).Replace('\\', '/'))
            .ToArray();

        Assert.Contains(imports, i => i.EndsWith("build/Publish.props", StringComparison.Ordinal));
    }

    [Fact]
    public void GitIgnoreExcludesTheArtifactsFolder()
    {
        string[] lines = File.ReadAllLines(RepoRoot.Combine(".gitignore")).Select(l => l.Trim()).ToArray();

        Assert.Contains("artifacts/", lines);
    }

    [Fact]
    public void RestartServiceStillResolvesTheExecutableThroughProcessPath()
    {
        string source = File.ReadAllText(
            RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Services", "AppRestartService.cs"));

        Assert.Contains("Environment.ProcessPath", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishPropsNeverEnablesSingleFileOrTrimming()
    {
        string text = File.ReadAllText(RepoRoot.Combine("build", "Publish.props"));

        Assert.DoesNotContain("<PublishSingleFile>true", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<PublishTrimmed>true", text, StringComparison.OrdinalIgnoreCase);
    }
}
