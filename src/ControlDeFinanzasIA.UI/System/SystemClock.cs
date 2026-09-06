using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.UI.System;

/// <summary>
/// Runtime implementation of <see cref="IClock"/>.
/// </summary>
/// <remarks>
/// Lives in <c>UI/System/</c> because reading the system time is a runtime
/// concern, not domain logic nor a heavy infrastructure detail (see RN-2 of
/// <c>spec/features/phase-01/F-008-composition-root.md</c>).
/// </remarks>
public sealed class SystemClock : IClock
{
    /// <summary>
    /// Gets the current date and time expressed in UTC.
    /// </summary>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Gets the current date, derived from <see cref="UtcNow"/>.
    /// </summary>
    /// <remarks>
    /// Calculated from UTC time to avoid time-zone inconsistencies across
    /// future features.
    /// </remarks>
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
