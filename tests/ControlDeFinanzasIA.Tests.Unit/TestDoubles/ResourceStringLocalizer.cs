using System.Globalization;
using ControlDeFinanzasIA.UI.Resources;
using Microsoft.Extensions.Localization;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// An <see cref="IStringLocalizer{T}"/> backed by the REAL <c>Strings</c> resources (es-DO and en), for tests that
/// must see the text the user sees. Unlike <see cref="FakeStringLocalizer{T}"/> it never echoes the key back.
/// </summary>
public sealed class ResourceStringLocalizer : IStringLocalizer<Strings>
{
    /// <inheritdoc />
    public LocalizedString this[string name]
    {
        get
        {
            string? value = Strings.ResourceManager.GetString(name, CultureInfo.CurrentUICulture);
            return new LocalizedString(name, value ?? name, value is null);
        }
    }

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            LocalizedString template = this[name];
            return new LocalizedString(name, string.Format(CultureInfo.CurrentCulture, template.Value, arguments), template.ResourceNotFound);
        }
    }

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
