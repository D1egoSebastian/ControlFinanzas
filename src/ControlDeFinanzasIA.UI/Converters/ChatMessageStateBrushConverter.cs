using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Maps a <see cref="ChatMessageState"/> to its design-token accent brush (F-015, D-10):
/// <see cref="ChatMessageState.Unavailable"/>/<see cref="ChatMessageState.Rejected"/> (something
/// failed or the input was invalid) → TerracottaBrush; <see cref="ChatMessageState.Cancelled"/>/
/// <see cref="ChatMessageState.NotConfigured"/> (a benign, user- or settings-driven note, not a
/// failure) → SageBrush; <see cref="ChatMessageState.Pending"/> → PaperDimBrush;
/// <see cref="ChatMessageState.Answered"/> (a real reply, not a muted note) → PaperBrush. No new
/// colors — the same tokens the budget-progress bar and anomaly chips already use.
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
            ChatMessageState.Unavailable or ChatMessageState.Rejected => "TerracottaBrush",
            ChatMessageState.Cancelled or ChatMessageState.NotConfigured => "SageBrush",
            ChatMessageState.Pending => "PaperDimBrush",
            _ => "PaperBrush",
        };

        if (Avalonia.Application.Current is { } app
            && app.TryGetResource(resourceKey, ThemeVariant.Default, out object? resource)
            && resource is IBrush brush)
        {
            return brush;
        }

        return Brushes.Transparent;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
