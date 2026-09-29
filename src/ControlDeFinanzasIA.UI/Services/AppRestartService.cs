using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.Services;

/// <summary>
/// <see cref="IAppRestartService"/> that relaunches the current executable and then shuts the
/// desktop lifetime down (F-019, D16, DD-8).
/// </summary>
/// <remarks>
/// Process launch and shutdown are injectable (internal constructor) so tests never spawn a
/// process or close a window. When the host is the <c>dotnet</c> muxer (a <c>dotnet run</c> or
/// <c>dotnet app.dll</c> session) the entry assembly path is passed as the first argument.
/// Every failure resolves to <see langword="false"/>; the method never throws.
/// </remarks>
public sealed class AppRestartService : IAppRestartService
{
    private readonly string? _processPath;
    private readonly string? _entryAssemblyPath;
    private readonly Func<ProcessStartInfo, bool> _launch;
    private readonly Func<bool> _shutdown;
    private readonly ILogger<AppRestartService> _logger;

    /// <summary>Initializes a new instance of the <see cref="AppRestartService"/> class.</summary>
    /// <param name="logger">Logger for relaunch failures.</param>
    public AppRestartService(ILogger<AppRestartService> logger)
        : this(Environment.ProcessPath, Assembly.GetEntryAssembly()?.Location, StartProcess, RequestShutdown, logger)
    {
    }

    internal AppRestartService(
        string? processPath,
        string? entryAssemblyPath,
        Func<ProcessStartInfo, bool> launch,
        Func<bool> shutdown,
        ILogger<AppRestartService> logger)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(shutdown);
        ArgumentNullException.ThrowIfNull(logger);

        _processPath = processPath;
        _entryAssemblyPath = entryAssemblyPath;
        _launch = launch;
        _shutdown = shutdown;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool TryRestart()
    {
        try
        {
            ProcessStartInfo? info = BuildStartInfo(_processPath, _entryAssemblyPath);
            if (info is null)
            {
                _logger.LogWarning("The application cannot restart itself: the executable path is unknown.");
                return false;
            }

            if (!_launch(info))
            {
                _logger.LogWarning("The application restart failed: the new process did not start.");
                return false;
            }

            if (!_shutdown())
            {
                _logger.LogWarning("The new instance started but the desktop lifetime could not be shut down.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The application restart failed.");
            return false;
        }
    }

    // Pure decision: which executable and arguments relaunch this app. Null when it cannot be built.
    internal static ProcessStartInfo? BuildStartInfo(string? processPath, string? entryAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return null;
        }

        var info = new ProcessStartInfo(processPath) { UseShellExecute = false };
        string? workingDirectory = Path.GetDirectoryName(processPath);

        bool isDotnetHost = string.Equals(
            Path.GetFileNameWithoutExtension(processPath),
            "dotnet",
            StringComparison.OrdinalIgnoreCase);
        if (isDotnetHost)
        {
            if (string.IsNullOrWhiteSpace(entryAssemblyPath))
            {
                return null;
            }

            info.ArgumentList.Add(entryAssemblyPath);
            workingDirectory = Path.GetDirectoryName(entryAssemblyPath);
        }

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            info.WorkingDirectory = workingDirectory;
        }

        return info;
    }

    private static bool StartProcess(ProcessStartInfo info)
    {
        using var process = Process.Start(info);
        return process is not null;
    }

    private static bool RequestShutdown()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
        {
            return false;
        }

        lifetime.Shutdown();
        return true;
    }
}
