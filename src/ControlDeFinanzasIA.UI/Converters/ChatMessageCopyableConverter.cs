using System.Globalization;
using Avalonia.Data.Converters;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Converts a <see cref="ChatMessageState"/> to whether its bubble's Copy button should be enabled
/// (F-015, R-13, D-9): every state except <see cref="ChatMessageState.Pending"/> ("Thinking…" has
/// nothing meaningful to copy yet). Bound directly to the item's own <c>State</c> property — which
/// raises <see cref="System.ComponentModel.INotifyPropertyChanged"/> when <c>Complete</c> resolves
/// it — instead of relying solely on <c>CopyCommand</c>'s generated <c>CanExecuteChanged</c>, which
/// a parameterized <c>[RelayCommand]</c> does not automatically re-raise when a property on the
/// bound item (rather than the view-model itself) changes.
/// </summary>
public sealed class ChatMessageCopyableConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static ChatMessageCopyableConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not ChatMessageState.Pending;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
