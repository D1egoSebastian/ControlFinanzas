using System.Globalization;
using Avalonia.Data.Converters;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Multi-value converter for the delete button of a list row. The button keeps its place in the row (so keyboard
/// focus is not lost) and changes its text when the row is armed for deletion.
/// </summary>
/// <remarks>
/// Values: the row identifier (<see cref="Guid"/>), the view-model's pending-delete identifier (<see cref="Guid"/>?)
/// and, for the <c>name</c> mode, the row description. The converter parameter selects the output:
/// <c>label</c> (the visible text) or <c>name</c> (the automation name, which includes the description so screen
/// reader users can tell the rows apart).
/// </remarks>
public sealed class DeleteRowTextConverter : IMultiValueConverter
{
    /// <summary>Gets the shared instance.</summary>
    public static DeleteRowTextConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        bool pending = RowPendingConverter.IsPending(values);

        if (string.Equals(parameter as string, "name", StringComparison.Ordinal))
        {
            string description = values.Count >= 3 ? values[2] as string ?? string.Empty : string.Empty;
            string format = pending ? Strings.A11yConfirmDeleteRowFormat : Strings.A11yDeleteRowFormat;
            return string.Format(culture, format, description);
        }

        return pending ? Strings.ConfirmDeleteButton : Strings.DeleteButton;
    }
}
