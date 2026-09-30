namespace ControlDeFinanzasIA.UI.Diagnostics;

/// <summary>Formatting of the correlation id shown to the user.</summary>
public static class CorrelationId
{
    private const int _shortLength = 8;

    /// <summary>Gets the short id: the first 8 lowercase hex characters of the Guid.</summary>
    /// <param name="id">The full correlation id.</param>
    /// <returns>Eight lowercase hex characters.</returns>
    public static string Short(Guid id) => id.ToString("N")[.._shortLength];
}
