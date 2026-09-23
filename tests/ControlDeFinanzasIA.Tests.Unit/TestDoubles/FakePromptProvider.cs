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

    /// <inheritdoc />
    public string GetTemplate(string name, string locale)
    {
        Calls.Add((name, locale));

        return string.Equals(name, PromptNames.CategorizeExpenseSystem, StringComparison.Ordinal)
            ? SystemTemplate
            : UserTemplate;
    }
}
