using System.Diagnostics;

namespace ControlDeFinanzasIA.Tests.Unit.Packaging;

/// <summary>Runs a repository script with whatever PowerShell the machine has (pwsh first, then Windows PowerShell).</summary>
internal static class PowerShellRunner
{
    public static (int ExitCode, string Output) Run(string scriptPath, params string[] arguments)
    {
        string shell = Locate();
        var info = new ProcessStartInfo(shell)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-ExecutionPolicy");
        info.ArgumentList.Add("Bypass");
        info.ArgumentList.Add("-File");
        info.ArgumentList.Add(scriptPath);
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
            throw new Xunit.Sdk.XunitException("The script did not finish within 120 s: " + scriptPath);
        }

        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    private static string Locate()
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
}
