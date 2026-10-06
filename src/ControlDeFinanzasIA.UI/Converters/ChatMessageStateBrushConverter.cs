using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Maps a <see cref="ChatMessageState"/> to its design-token text brush (F-015, D-10):
/// <see cref="ChatMessageState.Unavailable"/>/<see cref="ChatMessageState.Rejected"/> (something
/// failed or the input was invalid) → ExpenseBrush; <see cref="ChatMessageState.Cancelled"/>/
/// <see cref="ChatMessageState.NotConfigured"/> (a benign, user- or settings-driven note, not a
/// failure) → WarningBrush; <see cref="ChatMessageState.Pending"/> → TextMutedBrush;
/// <see cref="ChatMessageState.Answered"/> (a real reply, not a muted note) → TextBrush. All four are
/// text-safe tokens measured by <c>ContrastTests</c>; no new colors.
/// </summary>
public sealed class ChatMessageStateBrushConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static ChatMessageStateBrushConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value switch
        {
            ChatMessageState.Unavailable or ChatMessageState.Rejected => "ExpenseBrush",
            ChatMessageState.Cancelled or ChatMessageState.NotConfigured => "WarningBrush",
            ChatMessageState.Pending => "TextMutedBrush",
            _ => "TextBrush",
        };

        return ThemeBrushLookup.Find(resourceKey) ?? Brushes.Transparent;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
