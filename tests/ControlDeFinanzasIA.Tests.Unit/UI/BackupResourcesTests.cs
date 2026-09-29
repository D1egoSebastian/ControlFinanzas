using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using ControlDeFinanzasIA.UI.Resources;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Resx completeness for the F-019 backup strings (R-22, S-31): every key exists in the neutral
/// and es-DO resources and in the hand-maintained designer, none is empty, and every format
/// string formats with its documented number of arguments.
/// </summary>
public sealed class BackupResourcesTests
{
    // Key -> number of format arguments ({0}..{n-1}); 0 means the value is a plain string.
    private static readonly Dictionary<string, int> _expected = new()
    {
        ["BackupSectionTitle"] = 0,
        ["BackupSectionDescription"] = 0,
        ["BackupExportButton"] = 0,
        ["BackupRestoreButton"] = 0,
        ["BackupExportDialogTitle"] = 0,
        ["BackupRestoreDialogTitle"] = 0,
        ["BackupFileTypeName"] = 0,
        ["BackupWorkingExport"] = 0,
        ["BackupWorkingInspect"] = 0,
        ["BackupWorkingRestore"] = 0,
        ["BackupExportSucceeded"] = 1,
        ["BackupConfirmTitle"] = 0,
        ["BackupConfirmWarning"] = 0,
        ["BackupConfirmDetailsFormat"] = 2,
        ["BackupConfirmOlderSchemaNote"] = 0,
        ["BackupConfirmApiKeyLostNote"] = 0,
        ["BackupConfirmButton"] = 0,
        ["BackupCancelButton"] = 0,
        ["BackupRestoreSucceeded"] = 1,
        ["BackupRestartRequiredNotice"] = 0,
        ["BackupRestartNowButton"] = 0,
        ["BackupRestartFallback"] = 0,
        ["BackupApiKeyNotRestoredNotice"] = 0,
        ["BackupApiKeyKeptNotice"] = 0,
        ["BackupErrorNotABackup"] = 0,
        ["BackupErrorCorrupt"] = 0,
        ["BackupErrorNewerSchema"] = 4,
        ["BackupErrorIoError"] = 0,
        ["BackupErrorInsufficientSpace"] = 0,
    };

    private static readonly CultureInfo _es = CultureInfo.GetCultureInfo("es-DO");

    public static IEnumerable<object[]> Keys() => _expected.Keys.Select(k => new object[] { k });

    [Fact]
    public void Designer_ExposesExactlyTheExpectedBackupKeys()
    {
        string[] accessors = typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string) && p.Name.StartsWith("Backup", StringComparison.Ordinal))
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(_expected.Keys.Order(StringComparer.Ordinal), accessors);
    }

    [Fact]
    public void BothResources_DefineTheSameBackupKeySet()
    {
        string[] neutral = BackupKeysOf(CultureInfo.InvariantCulture);
        string[] es = BackupKeysOf(_es);

        Assert.Equal(_expected.Keys.Order(StringComparer.Ordinal), neutral);
        Assert.Equal(neutral, es);
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void Key_IsDefinedAndNonEmpty_InBothLocales_AndInTheDesigner(string key)
    {
        ResourceManager manager = Strings.ResourceManager;

        Assert.False(string.IsNullOrWhiteSpace(manager.GetString(key, CultureInfo.InvariantCulture)));
        Assert.False(string.IsNullOrWhiteSpace(manager.GetString(key, _es)));
        Assert.NotEqual(
            manager.GetString(key, CultureInfo.InvariantCulture),
            manager.GetString(key, _es));

        PropertyInfo? accessor = typeof(Strings).GetProperty(key, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(accessor);
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void Key_FormatsWithItsDocumentedArguments_InBothLocales(string key)
    {
        int argumentCount = _expected[key];
        object[] arguments = Enumerable.Range(0, argumentCount).Select(i => (object)$"arg{i}").ToArray();

        foreach (CultureInfo culture in new[] { CultureInfo.InvariantCulture, _es })
        {
            string template = Strings.ResourceManager.GetString(key, culture)!;
            string formatted = string.Format(culture, template, arguments);

            foreach (object argument in arguments)
            {
                Assert.Contains((string)argument, formatted);
            }

            // No placeholder beyond the documented arguments.
            Assert.DoesNotContain("{" + argumentCount + "}", template);
        }
    }

    [Fact]
    public void Accessors_FollowTheSelectedCulture_AndTheCultureIsRestored()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = _es;
            string spanish = Strings.BackupExportButton;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            string neutral = Strings.BackupExportButton;

            Assert.Equal("Crear respaldo", spanish);
            Assert.Equal("Create backup", neutral);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static string[] BackupKeysOf(CultureInfo culture)
    {
        ResourceSet? set = Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);
        return set!.Cast<DictionaryEntry>()
            .Select(e => (string)e.Key)
            .Where(k => k.StartsWith("Backup", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
