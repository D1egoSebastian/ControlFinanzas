using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Maps a <see cref="ChatMessageRole"/> to the horizontal alignment its chat bubble is rendered
/// at (F-015, D-10): <see cref="ChatMessageRole.User"/> → right, <see cref="ChatMessageRole.Assistant"/>
/// (real replies and system-generated notes alike) → left.
/// </summary>
public sealed class ChatMessageRoleAlignmentConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static ChatMessageRoleAlignmentConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ChatMessageRole.User ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
