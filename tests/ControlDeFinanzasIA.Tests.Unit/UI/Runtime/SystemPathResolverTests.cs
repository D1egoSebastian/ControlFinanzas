using ControlDeFinanzasIA.UI.System;

namespace ControlDeFinanzasIA.Tests.Unit.UI.Runtime;

public sealed class SystemPathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdf-pathresolver-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void ResolveDataDirectory_AbsoluteOverride_IsUsed() =>
        Assert.Equal(_root, SystemPathResolver.ResolveDataDirectory(_root));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/dir")]
    [InlineData("..\\somewhere")]
    public void ResolveDataDirectory_UnsetOrInvalidOverride_FallsBackToOsPath(string? value)
    {
        string fallback = SystemPathResolver.ResolveDataDirectory(null);

        Assert.Equal(fallback, SystemPathResolver.ResolveDataDirectory(value));
        Assert.EndsWith("ControlDeFinanzasIA", fallback);
        Assert.True(Path.IsPathRooted(fallback));
    }

    [Fact]
    public void Constructor_WithOverride_CreatesDirectoryAndExposesPaths()
    {
        string target = Path.Combine(_root, "nested");

        var resolver = new SystemPathResolver(target);

        Assert.True(Directory.Exists(target));
        Assert.Equal(target, resolver.DataDirectory);
        Assert.Equal(Path.Combine(target, "data.db"), resolver.DatabaseFilePath);
    }

    [Fact]
    public void PublicConstructor_ReadsEnvironmentVariable()
    {
        string? previous = Environment.GetEnvironmentVariable(SystemPathResolver.DataDirectoryEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(SystemPathResolver.DataDirectoryEnvironmentVariable, _root);

            Assert.Equal(_root, new SystemPathResolver().DataDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SystemPathResolver.DataDirectoryEnvironmentVariable, previous);
        }
    }
}
