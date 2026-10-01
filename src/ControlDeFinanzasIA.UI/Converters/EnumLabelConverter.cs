using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Converts the stored, language-neutral name of a frequency, rule type or status (for example <c>"Monthly"</c> or
/// <see cref="DebtPaymentStatus.Paid"/>) into its localized label. The stored value is never changed; only what the
/// user reads. An unknown value is shown as its own text so nothing is ever hidden.
/// </summary>
public sealed class EnumLabelConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static EnumLabelConverter Instance { get; } = new();

    /// <summary>Returns the localized label for <paramref name="value"/>.</summary>
    /// <param name="value">A name or an enum member.</param>
    /// <returns>The label.</returns>
    public static string Label(object? value) => value switch
    {
        null => string.Empty,
        DebtStatus.Active => Strings.EnumDebtStatusActive,
        DebtStatus.Closed => Strings.EnumDebtStatusClosed,
        DebtPaymentStatus.Pending => Strings.EnumPaymentStatusPending,
        DebtPaymentStatus.Paid => Strings.EnumPaymentStatusPaid,
        DebtPaymentStatus.Skipped => Strings.EnumPaymentStatusSkipped,
        "Monthly" => Strings.EnumFrequencyMonthly,
        "Bimonthly" => Strings.EnumFrequencyBimonthly,
        "Yearly" => Strings.EnumFrequencyYearly,
        "Biweekly" => Strings.EnumFrequencyBiweekly,
        "Subscription" => Strings.EnumRuleTypeSubscription,
        "FixedExpense" => Strings.EnumRuleTypeFixedExpense,
        _ => value.ToString() ?? string.Empty,
    };

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Label(value);

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new BindingNotification(new NotSupportedException(), BindingErrorType.Error);
}
