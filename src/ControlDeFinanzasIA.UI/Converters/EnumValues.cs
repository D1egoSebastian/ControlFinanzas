using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Provides ready-to-bind collections of enum values for Avalonia views.
/// </summary>
public static class EnumValues
{
    /// <summary>
    /// Gets the list of supported currencies for the expense form.
    /// </summary>
    public static IReadOnlyList<Currency> Currencies { get; } =
        Enum.GetValues(typeof(Currency)).Cast<Currency>().ToList();
}
