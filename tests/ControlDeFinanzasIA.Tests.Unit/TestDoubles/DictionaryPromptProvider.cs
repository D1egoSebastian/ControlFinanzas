using ControlDeFinanzasIA.Application.Abstractions.Llm;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IPromptProvider"/>, owned by Phase 16
/// (F-016, D-12). Maps a template <c>name</c> to a fixed template string
/// and records every call. Unlike <see cref="FakePromptProvider"/> (which
/// only distinguishes exactly two Phase 13 template names), this double
/// serves an arbitrary set of <c>(name → template)</c> pairs, which Phase
/// 16 needs for its own distinct system/user templates without editing the
/// shared Phase 13/14 fake (coexistence rule, D-12).
/// </summary>
public sealed class DictionaryPromptProvider : IPromptProvider
{
    /// <summary>Templates keyed by name, consulted by <see cref="GetTemplate"/>.</summary>
    public Dictionary<string, string> Templates { get; } = new(StringComparer.Ordinal);

    /// <summary>Every <c>(name, locale)</c> pair passed to <see cref="GetTemplate"/>, in call order.</summary>
    public List<(string Name, string Locale)> Calls { get; } = [];

    /// <inheritdoc />
    public string GetTemplate(string name, string locale)
    {
        Calls.Add((name, locale));

        return Templates.TryGetValue(name, out string? template) ? template : string.Empty;
    }
}
