using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Writes the next-charge date of a recurring rule ("Next charge: 11 Sep 2026") with the month name in the UI
/// language, so a numeric date such as 11/9 can never be read two different ways.
/// </summary>
public sealed class NextChargeTextConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static NextChargeTextConverter Instance { get; } = new();

    /// <summary>Formats a next-charge date in the UI language.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The "Next charge: ..." text.</returns>
    public static string Format(DateOnly date) =>
        string.Format(CultureInfo.CurrentUICulture, Strings.RecurringNextChargeFormat, date);

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateOnly date ? Format(date) : string.Empty;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new BindingNotification(new NotSupportedException(), BindingErrorType.Error);
}
