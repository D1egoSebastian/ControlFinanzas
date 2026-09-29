using ControlDeFinanzasIA.UI.Services;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Configurable <see cref="IAppRestartService"/> double (F-019): counts calls and returns a
/// configured result, so ViewModel tests never relaunch or close anything.
/// </summary>
public sealed class FakeAppRestartService : IAppRestartService
{
    /// <summary>Gets or sets the value returned by <see cref="TryRestart"/>.</summary>
    public bool Result { get; set; } = true;

    /// <summary>Gets the number of <see cref="TryRestart"/> calls.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public bool TryRestart()
    {
        Calls++;
        return Result;
    }
}
