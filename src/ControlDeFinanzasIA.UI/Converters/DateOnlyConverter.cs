using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Converts between <see cref="DateOnly" /> / <see cref="DateTime" /> (and nullable variants) and
/// <see cref="DateTimeOffset" /> so that an Avalonia <see cref="Avalonia.Controls.DatePicker" />
/// can be bound to ViewModel properties that expose <c>DateOnly?</c> or <c>DateTime?</c>.
/// </summary>
public sealed class DateOnlyConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return null;
        }

        if (value is DateOnly dateOnly)
        {
            return new DateTimeOffset(dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified));
        }

        if (value is DateTime dateTime)
        {
            return new DateTimeOffset(dateTime);
        }

        return new BindingNotification(new InvalidOperationException($"Unsupported date value: {value}"), BindingErrorType.Error);
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return null;
        }

        if (value is DateTimeOffset offset)
        {
            // A DateTime-typed property (for example the debt or recurring-rule start date) needs a DateTime back.
            Type target = Nullable.GetUnderlyingType(targetType) ?? targetType;
            return target == typeof(DateTime) ? offset.DateTime : DateOnly.FromDateTime(offset.DateTime);
        }

        if (value is DateTime dateTime)
        {
            return DateOnly.FromDateTime(dateTime);
        }

        return new BindingNotification(new InvalidOperationException($"Unsupported date value: {value}"), BindingErrorType.Error);
    }
}
