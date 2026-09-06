using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Converter that switches a row delete button label between
/// <see cref="Strings.DeleteButton"/> and <see cref="Strings.ConfirmDeleteButton"/>
/// based on whether the row id matches the view-model's pending delete id.
/// </summary>
/// <remarks>
/// Bound to <c>Content</c> on the row delete button. The <c>value</c> is the
/// VM's <c>PendingDelete*Id</c> (a nullable <see cref="Guid"/>), the
/// <c>parameter</c> is the row's <c>Id</c>. When they match, the button
/// is in confirmation mode and shows <c>Confirmar</c>; otherwise it shows
/// the default <c>Eliminar</c> label.
/// </remarks>
public sealed class DeleteRowLabelConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var pendingId = value as Guid?;
        var rowId = parameter as Guid?;

        if (pendingId.HasValue && rowId.HasValue && pendingId.Value == rowId.Value)
        {
            return Strings.ConfirmDeleteButton;
        }

        return Strings.DeleteButton;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new BindingNotification(new NotSupportedException(), BindingErrorType.Error);
}
