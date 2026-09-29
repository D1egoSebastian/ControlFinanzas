using System.Globalization;
using System.Reflection;
using System.Resources;
using ControlDeFinanzasIA.UI.Resources;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Resx completeness for the F-018 export strings (R-13, S-15): every accessor resolves to a
/// non-empty value in the neutral and es-DO resources, and es-DO defines its own entry.
/// </summary>
public sealed class ExportResourcesTests
{
    public static IEnumerable<object[]> ExportKeys() =>
        typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string)
                && (p.Name.StartsWith("Report", StringComparison.Ordinal) || p.Name.StartsWith("Export", StringComparison.Ordinal)))
            .Select(p => new object[] { p.Name });

    [Fact]
    public void ExportKeys_CoverTheDesignedSet() =>
        // 35 report labels (D-7), 9 UI keys and 4 error-code messages.
        Assert.Equal(48, ExportKeys().Count());

    [Theory]
    [MemberData(nameof(ExportKeys))]
    public void Key_IsDefinedAndNonEmpty_InBothLocales(string key)
    {
        ResourceManager manager = Strings.ResourceManager;
        var es = CultureInfo.GetCultureInfo("es-DO");

        Assert.False(string.IsNullOrWhiteSpace(manager.GetString(key, CultureInfo.InvariantCulture)));
        Assert.False(string.IsNullOrWhiteSpace(manager.GetString(key, es)));

        ResourceSet? esSet = manager.GetResourceSet(es, createIfNotExists: true, tryParents: false);
        Assert.NotNull(esSet);
        Assert.NotNull(esSet!.GetString(key));
    }
}
