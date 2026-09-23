using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Llm;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IPromptProvider"/> used in unit tests
/// (F-013, slice 5b, D-4). Keeps use case tests independent of the real
/// prompt wording shipped in Infrastructure — a prompt tweak must never
/// break a use case test.
/// </summary>
public sealed class FakePromptProvider : IPromptProvider
{
    /// <summary>Gets or sets the template returned for the system prompt name.</summary>
    public string SystemTemplate { get; set; } = "SYSTEM {{categories}}";

    /// <summary>Gets or sets the template returned for the user prompt name.</summary>
    public string UserTemplate { get; set; } = "USER {{description}} | {{categories}}";

    /// <summary>Every <c>(name, locale)</c> pair passed to <see cref="GetTemplate"/>, in call order.</summary>
    public List<(string Name, string Locale)> Calls { get; } = [];

    /// <summary>
    /// Per-name template override, consulted before the two-branch
    /// fallback below (F-014, slice 6, D-13). Additive: Phase 13 tests
    /// that never populate this dictionary keep using the fallback
    /// unchanged.
    /// </summary>
    public Dictionary<string, string> Templates { get; } = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string GetTemplate(string name, string locale)
    {
        Calls.Add((name, locale));

        if (Templates.TryGetValue(name, out string? overridden))
        {
            return overridden;
        }

        return string.Equals(name, PromptNames.CategorizeExpenseSystem, StringComparison.Ordinal)
            ? SystemTemplate
            : UserTemplate;
    }
}
