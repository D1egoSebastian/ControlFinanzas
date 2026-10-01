using System.Diagnostics;

namespace ControlDeFinanzasIA.Tests.Integration.Packaging;

/// <summary>Shared helpers for the F-021 S9 packaging tests that run against the real script output in <c>artifacts/</c>.</summary>
internal static class PackagingSupport
{
    public static readonly string[] Rids = ["win-x64", "osx-arm64", "osx-x64", "linux-x64"];

    private static readonly Lazy<string> _root = new(FindRoot);

    public static string RepoRoot => _root.Value;

    public static string Artifacts => Path.Combine(RepoRoot, "artifacts");

    /// <summary>Name of the environment variable that turns a missing <c>artifacts/</c> folder from a skip into a failure.</summary>
    public const string RequireVariable = "CONTROLDEFINANZASIA_REQUIRE_PACKAGING";

    /// <summary>
    /// Pure gate: the skip reason when the packaging output is absent and not required, otherwise <see langword="null"/>
    /// (the test runs; with the variable set and no output it then fails in <see cref="RequireArtifacts"/>).
    /// </summary>
    public static string? SkipReason(bool artifactsExist, string? requireValue)
    {
        if (artifactsExist || string.Equals(requireValue, "1", StringComparison.Ordinal))
        {
            return null;
        }

        return "Packaging artifacts are missing (artifacts/ not found). Run 'powershell -File scripts/publish.ps1 -Rid all' "
            + "to exercise this test, or set " + RequireVariable + "=1 to make a missing folder a failure.";
    }

    /// <summary>
    /// The packaging output is produced by <c>scripts/publish.ps1</c> and is git-ignored, so a fresh checkout does not
    /// have it. <see cref="PackagingFactAttribute"/> and <see cref="PackagingTheoryAttribute"/> skip the whole test when
    /// <c>artifacts/</c> is absent. Once the folder exists (or the variable forces the run) a missing file is a loud failure.
    /// </summary>
    public static void RequireArtifacts(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            throw new Xunit.Sdk.XunitException(
                "Packaging artifacts missing: " + path + ". Run 'powershell -File scripts/publish.ps1 -Rid all' first.");
        }
    }

    public static string Version()
    {
        var doc = System.Xml.Linq.XDocument.Load(Path.Combine(RepoRoot, "Directory.Build.props"));
        return doc.Descendants("Version").Single().Value.Trim();
    }

    public static (int ExitCode, string Output) RunScript(string scriptName, params string[] arguments)
    {
        string shell = LocateShell();
        var info = new ProcessStartInfo(shell)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string a in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(RepoRoot, "scripts", scriptName) })
        {
            info.ArgumentList.Add(a);
        }

        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(info)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new Xunit.Sdk.XunitException("The script did not finish within 120 s: " + scriptName);
        }

        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    private static string LocateShell()
    {
        string[] candidates = OperatingSystem.IsWindows() ? ["pwsh.exe", "powershell.exe"] : ["pwsh"];
        string[] folders = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (string candidate in candidates)
        {
            foreach (string folder in folders)
            {
                string full = Path.Combine(folder, candidate);
                if (File.Exists(full))
                {
                    return full;
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No PowerShell (pwsh or powershell.exe) was found on PATH.");
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ControlDeFinanzasIA.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root (ControlDeFinanzasIA.sln) not found.");
    }
}
