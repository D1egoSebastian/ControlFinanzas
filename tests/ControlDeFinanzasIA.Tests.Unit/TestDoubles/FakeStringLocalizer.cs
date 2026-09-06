using Microsoft.Extensions.Localization;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IStringLocalizer{T}"/> used in unit tests.
/// </summary>
/// <typeparam name="T">The marker type used by the localizer.</typeparam>
public sealed class FakeStringLocalizer<T> : IStringLocalizer<T>
{
    private readonly Dictionary<string, string> _values;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeStringLocalizer{T}"/> class.
    /// </summary>
    /// <param name="values">
    /// Optional dictionary of localized values. Missing keys fall back to the key name.
    /// </param>
    public FakeStringLocalizer(Dictionary<string, string>? values = null)
    {
        _values = values ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public LocalizedString this[string name]
    {
        get
        {
            bool found = _values.TryGetValue(name, out string? value);
            return new LocalizedString(name, found ? value! : name, !found);
        }
    }

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments] => this[name];

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => _values.Select(kvp => new LocalizedString(kvp.Key, kvp.Value));
}
