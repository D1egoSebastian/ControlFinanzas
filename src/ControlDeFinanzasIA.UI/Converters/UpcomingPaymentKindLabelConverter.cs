using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Converts an <see cref="UpcomingPaymentKind"/> into its localized label,
/// used by the Dashboard's "Próximos pagos" card (F-008, slice 3, R-6/R-7)
/// to tell a debt installment apart from a recurring-rule generation.
/// </summary>
public sealed class UpcomingPaymentKindLabelConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static UpcomingPaymentKindLabelConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is UpcomingPaymentKind.DebtInstallment)
        {
            return Strings.DashboardUpcomingPaymentKindDebt;
        }

        if (value is UpcomingPaymentKind.RecurringRule)
        {
            return Strings.DashboardUpcomingPaymentKindRecurring;
        }

        return new BindingNotification(new InvalidOperationException($"Unsupported kind: {value}"), BindingErrorType.Error);
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new BindingNotification(new NotSupportedException(), BindingErrorType.Error);
}
