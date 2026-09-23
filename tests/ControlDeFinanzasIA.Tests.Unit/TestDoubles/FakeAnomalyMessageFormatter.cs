using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IAnomalyMessageFormatter"/> used in unit
/// tests (F-014, slice S4). Returns a structural, non-localized string so
/// tests can assert the facts were passed through correctly without
/// coupling to real copy/locale (D-13).
/// </summary>
public sealed class FakeAnomalyMessageFormatter : IAnomalyMessageFormatter
{
    /// <summary>Facts passed to every <see cref="Format"/> call, in call order.</summary>
    public List<AnomalyMessageFacts> Calls { get; } = new();

    /// <inheritdoc />
    public string Format(AnomalyMessageFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        Calls.Add(facts);
        return $"{facts.CategoryName}|{facts.AmountDop}|{facts.Severity}";
    }
}
