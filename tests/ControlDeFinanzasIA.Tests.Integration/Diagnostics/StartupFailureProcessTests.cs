using System.Diagnostics;

namespace ControlDeFinanzasIA.Tests.Integration.Diagnostics;

/// <summary>
/// F-021 S3 (R-9, S-18): starts the REAL built UI application in a child process with
/// <c>CONTROLDEFINANZASIA_DATA_DIR</c> pointing at a regular file, which makes start-up fail for real (the data
/// directory cannot be created). No production test hook is involved. The report lands in the OS temp folder
/// because the preferred folder is unusable; the test deletes exactly the files it created.
/// </summary>
public sealed class StartupFailureProcessTests
{
    [Fact]
    public async Task RealApplicationWithAnUnusableDataDirectory_ExitsNonZero_WritesAReport_AndPrintsTheGuid()
    {
        string uiDll = Path.Combine(AppContext.BaseDirectory, "ControlDeFinanzasIA.UI.dll");
        Assert.True(File.Exists(uiDll), "The built UI assembly must be next to the test assembly: " + uiDll);

        string scratch = Path.Combine(Path.GetTempPath(), "cdfia-startup-proc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        string blocker = Path.Combine(scratch, "data-dir-is-a-file");
        File.WriteAllText(blocker, "regular file");
        string temp = Path.GetTempPath();
        HashSet<string> before = ListReports(temp);
        var created = new List<string>();
        try
        {
            var info = new ProcessStartInfo("dotnet")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            info.ArgumentList.Add(uiDll);
            info.Environment["CONTROLDEFINANZASIA_DATA_DIR"] = blocker;

            var watch = Stopwatch.StartNew();
            using Process process = Process.Start(info)!;
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new Xunit.Sdk.XunitException("The application did not exit within 60 s.");
            }

            string stderr = await stderrTask;
            _ = await stdoutTask;
            watch.Stop();

            created.AddRange(ListReports(temp).Except(before));

            Assert.NotEqual(0, process.ExitCode);
            string report = Assert.Single(created);
            Assert.Matches(@"^startup-error-\d{8}T\d{6}Z\.log$", Path.GetFileName(report));
            string content = File.ReadAllText(report);
            Assert.Matches(@"Error ID: [0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", content);
            string id = System.Text.RegularExpressions.Regex.Match(content, @"Error ID: (\S+)").Groups[1].Value;
            Assert.Contains(id, stderr, StringComparison.Ordinal);
            Assert.False(Directory.Exists(blocker));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60));
        }
        finally
        {
            foreach (string file in created)
            {
                File.Delete(file);
            }

            Directory.Delete(scratch, recursive: true);
        }
    }

    private static HashSet<string> ListReports(string directory) =>
        Directory.GetFiles(directory, "startup-error-*.log").ToHashSet(StringComparer.OrdinalIgnoreCase);
}
