using System.Globalization;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.UI.Formatting;

/// <summary>
/// Shared formatter for the app-wide "≈ {N2} DOP" pattern (D-5, Phase 9).
/// Extracted from <c>DashboardViewModel.FormatDop</c>, which already
/// documented itself as reused verbatim from
/// <c>ExpensesViewModel.EnrichWithConversionAsync</c> — <see cref="ApproximateDop"/>
/// is the third copy of this exact formatter, now consolidated here.
/// </summary>
internal static class MoneyFormat
{
    /// <summary>
    /// Formats a DOP amount using <see cref="Strings.ApproximateFormat"/>
    /// and <see cref="CultureInfo.CurrentCulture"/>.
    /// </summary>
    /// <param name="amount">The already-normalized DOP amount.</param>
    internal static string ApproximateDop(decimal amount)
        => string.Format(CultureInfo.CurrentCulture, Strings.ApproximateFormat, $"{amount:N2} DOP");
}
