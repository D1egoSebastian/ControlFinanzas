namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// A fact that only runs on Windows, where an open handle without <c>FileShare.Delete</c> blocks
/// <c>ReplaceFile</c>. On other systems it is reported as skipped (never silently passed).
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Requires Windows file-sharing semantics.";
        }
    }
}
