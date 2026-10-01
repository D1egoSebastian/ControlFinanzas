using ControlDeFinanzasIA.UI.Input;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Packaging;

/// <summary>F-021 S10 (R-26): the repository documents describe what Phase 21 really delivered and no more.</summary>
public sealed class DocsReconciledTests
{
    private static string Read(params string[] parts) => File.ReadAllText(RepoRoot.Combine(parts));

    private static string Section(string text, string startMarker, string endMarker)
    {
        int start = text.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Section not found: " + startMarker);
        int end = text.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, "Section end not found: " + endMarker);
        return text[start..end];
    }

    [Fact]
    public void ArchitectureLoggingSectionDescribesTheRealRollingFileLogger()
    {
        string logging = Section(Read("docs", "architecture.md"), "## 14. Logging", "## 15. Testing");

        Assert.Contains("RollingFileLoggerProvider", logging, StringComparison.Ordinal);
        Assert.Contains("app-YYYYMMDD", logging, StringComparison.Ordinal);
        Assert.Contains("5 MB", logging, StringComparison.Ordinal);
        Assert.Contains("UTC", logging, StringComparison.Ordinal);
        Assert.Contains("nunca lanza", logging, StringComparison.Ordinal);
        Assert.DoesNotContain("Serilog sink", logging, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UseSerilog", logging, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchitectureErrorHandlingSectionDescribesTheRealReporterHooksAndOverlay()
    {
        string errors = Section(Read("docs", "architecture.md"), "## 16. Manejo de errores", "## 17. Internacionalizaci");

        foreach (string name in new[] { "UnexpectedErrorReporter", "GlobalExceptionHooks", "overlay", "StartupGuard" })
        {
            Assert.Contains(name, errors, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("`IExceptionHandler` global", errors, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchitecturePackagingSectionDescribesTheRealLayoutAndTheDeferrals()
    {
        string packaging = Section(Read("docs", "architecture.md"), "## 18. Empaquetado", "## 19. Decisiones");

        foreach (string fact in new[] { "SHA256SUMS", "tar.gz", ".app.zip", "MSIX", "firma", "borrador", "no ejecutad" })
        {
            Assert.Contains(fact, packaging, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RoadmapRecordsPhase21AsImplementedWithTheInstallerDeviations()
    {
        string roadmap = Read("docs", "roadmap.md");
        string phase21 = roadmap[roadmap.IndexOf("### Fase 21", StringComparison.Ordinal)..];

        Assert.Contains("IMPLEMENTADA", phase21, StringComparison.Ordinal);
        Assert.Contains("MSIX", phase21, StringComparison.Ordinal);
        Assert.Contains("diferid", phase21, StringComparison.Ordinal);
        Assert.Contains("DMG", phase21, StringComparison.Ordinal);
        Assert.Contains("AppImage", phase21, StringComparison.Ordinal);
        Assert.Contains("verificado localmente", phase21, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sin verificar", phase21, StringComparison.Ordinal);
    }

    [Fact]
    public void NotionRoadmapNoLongerListsPhases19To21AsPending()
    {
        string notion = Read("docs", "roadmap-notion-ready.md");

        foreach (string phase in new[] { "| 19 |", "| 20 |", "| 21 |" })
        {
            string row = notion.Split('\n').Single(l => l.StartsWith(phase, StringComparison.Ordinal));
            Assert.DoesNotContain("Pendiente", row, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AgentsStatusMentionsPhase21AndThePackagingGateVariable()
    {
        string agents = Read("AGENTS.md");

        Assert.Contains("Phase 21", agents, StringComparison.Ordinal);
        Assert.Contains("CONTROLDEFINANZASIA_REQUIRE_PACKAGING", agents, StringComparison.Ordinal);
    }

    [Fact]
    public void PackagingUnverifiedDocumentsTheGateVariable()
    {
        string text = Read("docs", "packaging-unverified.md");

        Assert.Contains("CONTROLDEFINANZASIA_REQUIRE_PACKAGING", text, StringComparison.Ordinal);
        Assert.DoesNotContain("they return without checking", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadmeShortcutSummaryListsEveryCatalogGesture()
    {
        string readme = Read("README.md");

        foreach (KeyboardShortcut shortcut in KeyboardShortcutCatalog.All)
        {
            string gesture = KeyboardShortcutCatalog.Format(shortcut, isMacOS: false);

            // The README collapses Ctrl+1 ... Ctrl+9, Ctrl+0 into one row; the range ends must be literal.
            bool middleDigit = shortcut.SectionIndex is >= 2 and <= 8;
            if (!middleDigit)
            {
                Assert.Contains(gesture, readme, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void ReadmeDoesNotPresentTheUnrunWorkflowOrPlatformsAsVerified()
    {
        string readme = Read("README.md");

        Assert.Contains("never been run", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("produced by the release workflow", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangelogReleaseDoesNotClaimInstallersSigningOrAutoUpdate()
    {
        string changelog = Read("CHANGELOG.md");
        string release = changelog[changelog.IndexOf("## [1.0.0]", StringComparison.Ordinal)..];

        Assert.Contains("not code-signed", release, StringComparison.Ordinal);
        Assert.Contains("no installer", release, StringComparison.Ordinal);
        Assert.DoesNotContain("auto-update", release.Replace("no auto-update", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
    }
}
