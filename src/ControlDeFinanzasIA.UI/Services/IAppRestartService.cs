namespace ControlDeFinanzasIA.UI.Services;

/// <summary>
/// UI-layer port that relaunches the application (F-019, D16, DD-8).
/// </summary>
/// <remarks>
/// Used after a successful restore so the restored database is opened (and migrated) by a fresh
/// process. Lives in <c>UI/Services</c> because it needs the process and the desktop lifetime.
/// </remarks>
public interface IAppRestartService
{
    /// <summary>
    /// Starts a new instance of the application and then shuts the current one down.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the new instance started and shutdown was requested;
    /// <see langword="false"/> when the relaunch failed and the current process keeps running, so
    /// the caller can ask the user to close and reopen the app. Never throws.
    /// </returns>
    bool TryRestart();
}
