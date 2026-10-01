using System.Globalization;
using Avalonia.Data.Converters;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Multi-value converter for list-row templates: <c>true</c> when the row is the one awaiting delete confirmation.
/// </summary>
/// <remarks>
/// Values: the row identifier (<see cref="Guid"/>) and the view-model's pending-delete identifier
/// (<see cref="Guid"/>?). The pair is bound from inside a row template, where the row — not the view-model — is the
/// data context.
/// </remarks>
public sealed class RowPendingConverter : IMultiValueConverter
{
    /// <summary>Gets the shared instance.</summary>
    public static RowPendingConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        IsPending(values);

    internal static bool IsPending(IList<object?> values) =>
        values.Count >= 2
        && values[0] is Guid rowId
        && values[1] is Guid pendingId
        && rowId == pendingId;
}
