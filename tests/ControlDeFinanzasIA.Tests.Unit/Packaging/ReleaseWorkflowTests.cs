using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Packaging;

/// <summary>
/// F-021 S9: structural checks of <c>.github/workflows/release.yml</c> (R-24, S-47, T16). No YAML library exists in
/// the test project and no package may be added, so the file is checked with anchored text rules; a real YAML parse
/// and the execution of the workflow are NOT covered here (documented in docs/packaging-unverified.md).
/// </summary>
public sealed class ReleaseWorkflowTests
{
    private static string[] Lines() => File.ReadAllLines(RepoRoot.Combine(".github", "workflows", "release.yml"));

    private static string Text() => File.ReadAllText(RepoRoot.Combine(".github", "workflows", "release.yml"));

    [Fact]
    public void WorkflowFileExistsAndIsTagTriggeredOnly()
    {
        string[] lines = Lines();
        int on = Array.FindIndex(lines, l => l == "on:");
        Assert.True(on >= 0, "top-level 'on:' not found");

        // Everything indented under 'on:' up to the next top-level key.
        string[] block = lines.Skip(on + 1).TakeWhile(l => l.Length == 0 || l.StartsWith(' ') || l.StartsWith('#')).ToArray();
        string[] keys = block.Where(l => Regex.IsMatch(l, @"^ {2}\S")).Select(l => l.Trim()).ToArray();

        Assert.Equal("push:", Assert.Single(keys));
        Assert.Contains(block, l => l.Trim() == "tags: ['v*']");
        Assert.DoesNotContain(block, l => l.Contains("branches", StringComparison.Ordinal));
        Assert.Empty(Regex.Matches(Text(), @"(?m)^\s*(pull_request|workflow_dispatch|schedule|release)\s*:"));
    }

    [Fact]
    public void FirstJobFailsUnlessTheTagEqualsVPlusThePropsVersion()
    {
        string text = Text();
        string version = System.Xml.Linq.XDocument.Load(RepoRoot.Combine("Directory.Build.props"))
            .Descendants("Version").Single().Value.Trim();

        int jobs = text.IndexOf("\njobs:", StringComparison.Ordinal);
        Assert.True(jobs > 0, "jobs: not found");
        Match first = Regex.Match(text[(jobs + 1)..], @"(?m)^ {2}([A-Za-z][\w-]*):");
        Assert.Equal("check-tag", first.Groups[1].Value);

        Assert.Contains("GITHUB_REF_NAME", text, StringComparison.Ordinal);
        Assert.Contains("Directory.Build.props", text, StringComparison.Ordinal);
        Assert.Contains("exit 1", text, StringComparison.Ordinal);
        Assert.DoesNotContain(version, text, StringComparison.Ordinal); // never typed, always read from the props
        Assert.Matches(@"(?m)^\s+needs: \[?check-tag\]?\s*$", text);
    }

    [Fact]
    public void ReleaseIsCreatedAsADraftWithTheGhCliAndNoThirdPartyReleaseAction()
    {
        string text = Text();

        Assert.Matches(@"gh release create .*--draft", text);
        Assert.DoesNotContain("--draft=false", text, StringComparison.Ordinal);
        Assert.DoesNotContain("--latest", text, StringComparison.Ordinal);

        string[] allowed = ["actions/checkout@", "actions/setup-dotnet@", "actions/upload-artifact@", "actions/download-artifact@"];
        foreach (Match m in Regex.Matches(text, @"(?m)^\s*-?\s*uses:\s*(\S+)"))
        {
            Assert.Contains(allowed, a => m.Groups[1].Value.StartsWith(a, StringComparison.Ordinal));
        }

        Assert.DoesNotContain("secrets.", text.Replace("secrets.GITHUB_TOKEN", string.Empty), StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheLastJobCanWriteContents()
    {
        string text = Text();
        int jobsAt = text.IndexOf("\njobs:", StringComparison.Ordinal);
        string jobsText = text[jobsAt..];
        MatchCollection jobs = Regex.Matches(jobsText, @"(?m)^ {2}([A-Za-z][\w-]*):\s*$");
        string[] names = jobs.Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal("publish-draft", names[^1]);

        for (int i = 0; i < jobs.Count; i++)
        {
            int start = jobs[i].Index;
            int end = i + 1 < jobs.Count ? jobs[i + 1].Index : jobsText.Length;
            string body = jobsText[start..end];
            bool isLast = i == jobs.Count - 1;

            Assert.Contains(isLast ? "contents: write" : "contents: read", body, StringComparison.Ordinal);
            if (!isLast)
            {
                Assert.DoesNotContain("contents: write", body, StringComparison.Ordinal);
            }
        }

        // No workflow-level permissions block that could grant write to every job.
        Assert.Empty(Regex.Matches(text, @"(?m)^permissions:\s*\S"));
    }

    [Fact]
    public void WorkflowCoversTheThreeRunnerFamilies()
    {
        string text = Text();

        Assert.Contains("windows-latest", text, StringComparison.Ordinal);
        Assert.Contains("macos-14", text, StringComparison.Ordinal);
        Assert.Contains("ubuntu-latest", text, StringComparison.Ordinal);
        Assert.Contains("smoke-win.ps1", text, StringComparison.Ordinal);
        Assert.Contains("hdiutil", text, StringComparison.Ordinal);
        Assert.Contains("build-appimage.sh", text, StringComparison.Ordinal);
        Assert.Contains("verify-layout.ps1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildWorkflowIsUnchangedComparedToHead()
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = RepoRoot.Path,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string a in new[] { "diff", "--quiet", "HEAD", "--", ".github/workflows/build.yml" })
        {
            info.ArgumentList.Add(a);
        }

        using Process p = Process.Start(info)!;
        p.WaitForExit(30_000);

        Assert.Equal(0, p.ExitCode);
    }
}
