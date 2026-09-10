using System.Globalization;
using Avalonia.Data.Converters;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// DEBUG-ONLY converter that returns the runtime type name of a value.
/// Used temporarily in <see cref="Views.MainWindow"/> to diagnose
/// why the ExpensesView does not render. Remove this converter and
/// its usages once the issue is resolved.
/// </summary>
public sealed class DebugTypeConverter : IValueConverter
{
    /// <summary>Singleton instance.</summary>
    public static readonly DebugTypeConverter Instance = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.GetType().Name ?? "(null)";

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
