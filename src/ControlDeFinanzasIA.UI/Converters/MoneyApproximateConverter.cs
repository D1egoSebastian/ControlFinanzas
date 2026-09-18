using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Formats a <see cref="Money"/> with the app-wide "≈ {0}" pattern
/// (<see cref="Strings.ApproximateFormat"/>), reused verbatim from
/// <c>DashboardViewModel.FormatDop</c> but per-item and in the item's own
/// currency (no conversion). Used by the Dashboard's "Próximos pagos" card
/// (F-008, slice 3): each <c>UpcomingPaymentItem.Amount</c> is shown
/// individually, not aggregated, so it is never normalized to DOP.
/// </summary>
public sealed class MoneyApproximateConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static MoneyApproximateConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Money money)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                Strings.ApproximateFormat,
                $"{money.Amount:N2} {money.Currency}");
        }

        return new BindingNotification(new InvalidOperationException($"Unsupported value: {value}"), BindingErrorType.Error);
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new BindingNotification(new NotSupportedException(), BindingErrorType.Error);
}
