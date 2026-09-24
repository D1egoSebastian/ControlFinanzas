using System.Globalization;
using Avalonia.Data.Converters;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Maps a <see cref="ChatMessageRole"/> to its localized bubble label (F-015, D-10, D-11):
/// <see cref="ChatMessageRole.User"/> → <see cref="Strings.ChatYouLabel"/>,
/// <see cref="ChatMessageRole.Assistant"/> → <see cref="Strings.ChatAssistantLabel"/>. Reads
/// through <see cref="Strings"/> directly (not a fixed string) so the label follows the active
/// UI culture, same as every other localized bubble/note text.
/// </summary>
public sealed class ChatMessageRoleLabelConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static ChatMessageRoleLabelConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ChatMessageRole.User ? Strings.ChatYouLabel : Strings.ChatAssistantLabel;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
