using System.Text.Json;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Packaging;

/// <summary>
/// F-021 verify W4 (R-5 spirit: test and design-time tooling must not ship): the real publish output of each RID must
/// not contain Avalonia.Headless, the EF Core design-time tooling or the Roslyn / T4 assemblies it drags in. A missing
/// <c>artifacts/</c> folder skips the cases (see <see cref="PackagingSupport.SkipReason"/>).
/// </summary>
[Trait("Category", "Packaging")]
public sealed class PublishPayloadTests
{
    private static readonly string[] _forbiddenPrefixes =
    [
        "Avalonia.Headless",
        "Microsoft.EntityFrameworkCore.Design",
        "Microsoft.CodeAnalysis",
        "Mono.TextTemplating",
    ];

    public static TheoryData<string> AllRids() => PublishLayoutTests.AllRids();

    [PackagingTheory]
    [MemberData(nameof(AllRids))]
    public void RealPublishOutputContainsNoTestOrDesignTimeAssemblies(string rid)
    {
        string folder = Path.Combine(PackagingSupport.Artifacts, "publish", rid);
        PackagingSupport.RequireArtifacts(folder);

        string[] offenders = Directory.EnumerateFiles(folder, "*.dll", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(name => name is not null && _forbiddenPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .Select(name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(offenders.Length == 0, "Test/design-time assemblies in the " + rid + " payload: " + string.Join(", ", offenders));
    }

    [PackagingTheory]
    [MemberData(nameof(AllRids))]
    public void RealPublishDepsManifestDeclaresNoRuntimeAssetsForThem(string rid)
    {
        string deps = Path.Combine(PackagingSupport.Artifacts, "publish", rid, "ControlDeFinanzasIA.UI.deps.json");
        PackagingSupport.RequireArtifacts(deps);

        // The manifest may still NAME a package (as a dependency edge or an asset-less entry); a runtime, runtimeTargets or
        // resources asset would make the host look for a file that is not there.
        using var doc = JsonDocument.Parse(File.ReadAllText(deps));
        var offenders = new List<string>();
        foreach (JsonProperty target in doc.RootElement.GetProperty("targets").EnumerateObject())
        {
            foreach (JsonProperty library in target.Value.EnumerateObject())
            {
                if (!_forbiddenPrefixes.Any(p => library.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (new[] { "runtime", "runtimeTargets", "resources", "native" }.Any(k => library.Value.TryGetProperty(k, out _)))
                {
                    offenders.Add(library.Name);
                }
            }
        }

        Assert.True(offenders.Count == 0, "deps.json declares runtime assets for: " + string.Join(", ", offenders));
    }
}
