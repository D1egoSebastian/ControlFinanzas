using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Packaging;

/// <summary>The skip-or-fail gate of the real-artifact packaging tests (missing <c>artifacts/</c> on a fresh clone).</summary>
public sealed class PackagingGateTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("true")]
    public void MissingArtifactsSkipWithAnExplicitMessageUnlessRequired(string? value)
    {
        string? reason = PackagingSupport.SkipReason(artifactsExist: false, requireValue: value);

        Assert.NotNull(reason);
        Assert.Contains("artifacts/", reason, StringComparison.Ordinal);
        Assert.Contains("scripts/publish.ps1", reason, StringComparison.Ordinal);
        Assert.Contains(PackagingSupport.RequireVariable, reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RequireVariableSetToOneDoesNotSkipSoTheMissingFolderFails()
    {
        Assert.Null(PackagingSupport.SkipReason(artifactsExist: false, requireValue: "1"));

        string missing = Path.Combine(Path.GetTempPath(), "cdfia-no-artifacts-" + Guid.NewGuid().ToString("N"));
        Xunit.Sdk.XunitException ex = Assert.Throws<Xunit.Sdk.XunitException>(() => PackagingSupport.RequireArtifacts(missing));
        Assert.Contains("Packaging artifacts missing", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public void ExistingArtifactsAlwaysRun(string? value)
        => Assert.Null(PackagingSupport.SkipReason(artifactsExist: true, requireValue: value));

    [Fact]
    public void TheEnvironmentVariableNameIsTheDocumentedOne()
        => Assert.Equal("CONTROLDEFINANZASIA_REQUIRE_PACKAGING", PackagingSupport.RequireVariable);
}
