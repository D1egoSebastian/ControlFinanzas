using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ControlDeFinanzasIA.Domain.Debts;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Maps a debt or installment status to the colours of its status chip: paid and closed read as income green, skipped
/// as amber, an active debt as cobalt and a pending installment as a quiet neutral. The converter parameter picks the
/// fill (<c>bg</c>) or the text (<c>fg</c>). Every colour is a token (looked up in the active theme variant) that ContrastTests already measure as a pair, in both variants.
/// </summary>
public sealed class StatusToneConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static StatusToneConverter Instance { get; } = new();

    /// <summary>Returns the (fill, text) brush resource keys of a status chip.</summary>
    /// <param name="status">A <see cref="DebtStatus"/> or <see cref="DebtPaymentStatus"/> member.</param>
    /// <returns>The resource keys of the fill and of the text.</returns>
    public static (string Background, string Foreground) ToneKeys(object? status) => status switch
    {
        DebtPaymentStatus.Paid or DebtStatus.Closed => ("IncomeSoftBrush", "IncomeBrush"),
        DebtPaymentStatus.Skipped => ("WarningSoftBrush", "WarningBrush"),
        DebtStatus.Active => ("AccentSoftBrush", "AccentBrush"),
        _ => ("SurfaceMutedBrush", "TextSecondaryBrush"),
    };

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        (string background, string foreground) = ToneKeys(value);
        string key = string.Equals(parameter as string, "bg", StringComparison.Ordinal) ? background : foreground;

        return ThemeBrushLookup.Find(key) ?? Brushes.Transparent;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
