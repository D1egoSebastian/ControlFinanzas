using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.UI.Formatting;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI.Formatting;

/// <summary>
/// Unit tests for <see cref="ResxAnomalyMessageFormatter"/> (F-014, R-8 vs
/// D-9, slice S7). Verifies the port implementation resolves
/// <c>Strings.DashboardAnomalyMessageFormat</c>, is pure, and never calls
/// an LLM (there is nothing to call — it is a plain string formatter).
/// </summary>
public sealed class ResxAnomalyMessageFormatterTests
{
    [Fact]
    public void Format_ReturnsTextContainingCategoryAndFormattedAmounts()
    {
        var formatter = new ResxAnomalyMessageFormatter();
        var facts = new AnomalyMessageFacts(
            CategoryName: "Supermercado",
            AmountDop: 12500m,
            MedianDop: 2300m,
            RatioToMedian: 5.43m,
            Severity: AnomalySeverity.Warning);

        string message = formatter.Format(facts);

        Assert.Contains("Supermercado", message, StringComparison.Ordinal);
        Assert.Contains("5.43", message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.True(message.Length <= AnomalyAlert.MaxMessageLength);
    }

    [Fact]
    public void Format_IsPure_SameFactsProduceSameMessage()
    {
        var formatter = new ResxAnomalyMessageFormatter();
        var facts = new AnomalyMessageFacts("Transporte", 900m, 300m, 3.00m, AnomalySeverity.Info);

        string first = formatter.Format(facts);
        string second = formatter.Format(facts);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Format_NullFacts_Throws()
    {
        var formatter = new ResxAnomalyMessageFormatter();

        Assert.Throws<ArgumentNullException>(() => formatter.Format(null!));
    }
}
