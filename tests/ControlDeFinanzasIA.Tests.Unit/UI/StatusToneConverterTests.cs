using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.UI.Converters;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>The status chip colours of the debt screens come from tokens that ContrastTests already measure.</summary>
public sealed class StatusToneConverterTests
{
    [Theory]
    [InlineData(DebtPaymentStatus.Pending, "SurfaceMutedBrush", "TextSecondaryBrush")]
    [InlineData(DebtPaymentStatus.Paid, "IncomeSoftBrush", "IncomeBrush")]
    [InlineData(DebtPaymentStatus.Skipped, "WarningSoftBrush", "WarningBrush")]
    [InlineData(DebtStatus.Active, "AccentSoftBrush", "AccentBrush")]
    [InlineData(DebtStatus.Closed, "IncomeSoftBrush", "IncomeBrush")]
    [InlineData(null, "SurfaceMutedBrush", "TextSecondaryBrush")]
    public void ToneKeys_MapEveryStatusToATokenPair(object? status, string background, string foreground) => Assert.Equal((background, foreground), StatusToneConverter.ToneKeys(status));
}
