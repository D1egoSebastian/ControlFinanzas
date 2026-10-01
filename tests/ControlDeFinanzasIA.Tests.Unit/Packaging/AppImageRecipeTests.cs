using System.Text.RegularExpressions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Packaging;

/// <summary>F-021 S9: the AppImage recipe files exist and are internally consistent (R-23, S-46). No AppImage is built here.</summary>
public sealed class AppImageRecipeTests
{
    private static string Linux(string name) => RepoRoot.Combine("packaging", "linux", name);

    [Fact]
    public void RecipeFilesExist()
    {
        foreach (string name in new[] { "AppRun", "controldefinanzasia.desktop", "build-appimage.sh" })
        {
            Assert.True(File.Exists(Linux(name)), name);
        }
    }

    [Fact]
    public void DesktopEntryHasTheRequiredKeys()
    {
        var keys = File.ReadAllLines(Linux("controldefinanzasia.desktop"))
            .Where(l => l.Contains('=') && !l.TrimStart().StartsWith('#'))
            .Select(l => l.Split('=', 2))
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

        Assert.Equal("Application", keys["Type"]);
        Assert.Equal("ControlDeFinanzasIA", keys["Name"]);
        Assert.Equal("ControlDeFinanzasIA.UI", keys["Exec"]);
        Assert.Equal("controldefinanzasia", keys["Icon"]);
        Assert.EndsWith(";", keys["Categories"], StringComparison.Ordinal);
        Assert.Contains("Office", keys["Categories"], StringComparison.Ordinal);
    }

    [Fact]
    public void AppRunIsAShellScriptWithUnixLineEndingsThatLaunchesTheExecutable()
    {
        string text = File.ReadAllText(Linux("AppRun"));

        Assert.StartsWith("#!/bin/sh", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
        Assert.Contains("ControlDeFinanzasIA.UI", text, StringComparison.Ordinal);
        Assert.Contains("exec ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildScriptReferencesOnlyFilesThatExistOrAreProducedEarlierInTheScript()
    {
        string script = File.ReadAllText(Linux("build-appimage.sh"));

        Assert.StartsWith("#!/bin/sh", script, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', script);
        Assert.Contains("appimagetool", script, StringComparison.Ordinal);

        // Repository files the script copies in.
        foreach (Match m in Regex.Matches(script, @"(?:packaging/linux|src/ControlDeFinanzasIA\.UI/Assets/Icon)/[A-Za-z0-9._-]+"))
        {
            Assert.True(File.Exists(RepoRoot.Combine(m.Value.Split('/'))), "Referenced but missing: " + m.Value);
        }

        // The publish input is produced by scripts/publish.ps1 and the script must fail loudly when it is absent.
        Assert.Contains("artifacts/publish/linux-x64", script, StringComparison.Ordinal);
        Assert.Contains("exit 1", script, StringComparison.Ordinal);
        Assert.True(File.Exists(RepoRoot.Combine("scripts", "publish.ps1")));

        // The icon named in the desktop entry is the one copied into the AppDir.
        Assert.Contains("controldefinanzasia.png", script, StringComparison.Ordinal);
    }
}
