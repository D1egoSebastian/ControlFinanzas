using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Packaging;

/// <summary>F-021 S1: README, CHANGELOG and LICENSE exist with the required content (R-1, S-2, T14, T15).</summary>
public sealed class RepoDocsTests
{
    private static string Read(string name) => File.ReadAllText(RepoRoot.Combine(name));

    [Fact]
    public void ChangelogHasUnreleasedAndReleaseSectionsWithKeepAChangelogCategories()
    {
        string text = Read("CHANGELOG.md");

        Assert.Contains("## [Unreleased]", text);
        Assert.Contains("## [1.0.0]", text);
        Assert.Contains("### Added", text);

        string release = text[text.IndexOf("## [1.0.0]", StringComparison.Ordinal)..];
        foreach (string area in new[]
        {
            "expense", "income", "categor", "budget", "goal", "debt", "recurring", "calendar", "dashboard",
            "AI", "chat", "notification", "export", "backup", "import", "rolling log", "accessib", "shortcut", "packag",
        })
        {
            Assert.Contains(area, release, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LicenseIsAllRightsReserved()
    {
        Assert.True(File.Exists(RepoRoot.Combine("LICENSE")));
        Assert.Contains("All rights reserved", Read("LICENSE"));
    }

    [Fact]
    public void ReadmeHasEveryRequiredSection()
    {
        string text = Read("README.md");

        foreach (string heading in new[]
        {
            "## What it is", "## Download and run", "### Windows", "### macOS", "### Linux", "## Build from source",
            "## Data directory", "## Keyboard shortcuts", "### Windows SmartScreen", "### macOS Gatekeeper",
        })
        {
            Assert.Contains(heading, text);
        }

        Assert.Contains("ControlDeFinanzasIA.UI", text);
        Assert.Contains("CONTROLDEFINANZASIA_DATA_DIR", text);
        Assert.Contains("provided, untested", text);
        Assert.Contains("ICU", text);
        Assert.Contains("libicu", text);
    }

    [Fact]
    public void ReadmeShortcutSummaryListsTheSpecGestures()
    {
        string text = Read("README.md");

        foreach (string gesture in new[]
        {
            "Ctrl+N", "Ctrl+Shift+N", "Ctrl+1", "Ctrl+9", "Ctrl+0", "Ctrl+,", "Ctrl+Shift+B", "Esc",
        })
        {
            Assert.Contains(gesture, text);
        }
    }
}
