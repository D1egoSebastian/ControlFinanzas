using System.Runtime.InteropServices;
using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.UI.System;

/// <summary>
/// Runtime implementation of <see cref="IPathResolver"/>.
/// </summary>
/// <remarks>
/// Lives in <c>UI/System/</c> because resolving OS-specific paths and
/// creating the data directory are runtime concerns, not domain logic nor
/// heavy infrastructure details (see RN-2 of
/// <c>spec/features/phase-01/F-008-composition-root.md</c>).
/// </remarks>
public sealed class SystemPathResolver : IPathResolver
{
    private readonly string _dataDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemPathResolver"/> class
    /// and ensures the data directory exists.
    /// </summary>
    /// <remarks>
    /// The constructor performs I/O by calling <see cref="Directory.CreateDirectory(string)"/>.
    /// This is a documented exception to the "no I/O in constructors" rule:
    /// the SQLite database file will be created in this directory in F-002 of
    /// Phase 2, so the directory must exist before the app starts (see RN-4 of
    /// <c>spec/features/phase-01/F-008-composition-root.md</c>).
    /// </remarks>
    public SystemPathResolver()
    {
        _dataDirectory = ResolveDataDirectory();

        try
        {
            Directory.CreateDirectory(_dataDirectory);
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"Failed to create data directory '{_dataDirectory}': {ex.Message}");
            throw;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Failed to create data directory '{_dataDirectory}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the directory where the application persists its data.
    /// </summary>
    /// <remarks>
    /// The path follows the conventions documented in
    /// <c>spec/constitution/tech-stack.md</c>:
    /// <list type="bullet">
    /// <item>Windows: <c>%APPDATA%\ControlDeFinanzasIA</c>.</item>
    /// <item>macOS: <c>~/Library/Application Support/ControlDeFinanzasIA</c>.</item>
    /// <item>Linux: <c>$XDG_CONFIG_HOME/ControlDeFinanzasIA</c> or <c>~/.config/ControlDeFinanzasIA</c>.</item>
    /// </list>
    /// </remarks>
    public string DataDirectory => _dataDirectory;

    /// <summary>
    /// Gets the full path to the SQLite database file.
    /// </summary>
    public string DatabaseFilePath => Path.Combine(_dataDirectory, "data.db");

    private static string ResolveDataDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ControlDeFinanzasIA");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                "ControlDeFinanzasIA");
        }

        string? xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdgConfigHome))
        {
            return Path.Combine(xdgConfigHome, "ControlDeFinanzasIA");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "ControlDeFinanzasIA");
    }
}
