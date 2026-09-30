using System.Reflection;

namespace ControlDeFinanzasIA.UI.Diagnostics;

/// <summary>
/// The single reader of the application version. The value comes from the one <c>&lt;Version&gt;</c> element in
/// <c>Directory.Build.props</c>; the source-revision suffix is disabled there, so it is exactly the release version.
/// </summary>
public static class AppVersionInfo
{
    private const string _fallbackVersion = "0.0.0";

    /// <summary>Gets the informational version of the UI assembly (for example <c>1.0.0</c>).</summary>
    public static string Current { get; } =
        typeof(AppVersionInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? _fallbackVersion;
}
