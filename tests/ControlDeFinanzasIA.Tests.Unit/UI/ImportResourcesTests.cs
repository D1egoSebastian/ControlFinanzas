using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.UI.Resources;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Resx completeness for the F-020 import strings (R-24, S-27), following the Backup/Export pattern: every
/// key exists in the neutral and es-DO resources and in the hand-maintained designer, none is empty, every
/// format string formats with its documented number of arguments, and every <see cref="ImportReason"/>
/// value has a localized message so a new value cannot ship without one.
/// </summary>
public sealed class ImportResourcesTests
{
    // Key -> number of format arguments ({0}..{n-1}); 0 means the value is a plain string.
    private static readonly Dictionary<string, int> _expected = new()
    {
        ["ImportButton"] = 0,
        ["ImportDialogTitle"] = 0,
        ["FileDialogAllSupportedTypes"] = 0,
        ["ImportFileTypeCsv"] = 0,
        ["ImportFileTypeText"] = 0,
        ["ImportFileTypeExcel"] = 0,
        ["ImportTitle"] = 0,
        ["ImportSourceFactsCsvFormat"] = 3,
        ["ImportSourceFactsXlsxFormat"] = 2,
        ["ImportEncodingFallbackBanner"] = 1,
        ["ImportDecimalConflictBanner"] = 0,
        ["ImportDecimalAmbiguousBanner"] = 0,
        ["ImportDateAmbiguousBanner"] = 0,
        ["ImportDateContradictsBanner"] = 0,
        ["ImportFieldDate"] = 0,
        ["ImportFieldType"] = 0,
        ["ImportFieldDescription"] = 0,
        ["ImportFieldCategory"] = 0,
        ["ImportFieldAmount"] = 0,
        ["ImportFieldCurrency"] = 0,
        ["ImportFieldPaymentMethod"] = 0,
        ["ImportColumnNone"] = 0,
        ["ImportMappingTitle"] = 0,
        ["ImportMappingToggle"] = 0,
        ["ImportDelimiterLabel"] = 0,
        ["ImportDelimiterAuto"] = 0,
        ["ImportDelimiterComma"] = 0,
        ["ImportDelimiterSemicolon"] = 0,
        ["ImportDelimiterTab"] = 0,
        ["ImportDecimalStyleLabel"] = 0,
        ["ImportDecimalStyleAuto"] = 0,
        ["ImportDecimalStyleAutoDetectedFormat"] = 1,
        ["ImportDecimalStylePoint"] = 0,
        ["ImportDecimalStyleComma"] = 0,
        ["ImportDateFormatLabel"] = 0,
        ["ImportDateFormatDayMonthYear"] = 0,
        ["ImportDateFormatMonthDayYear"] = 0,
        ["ImportDateFormatDayMonthYearDash"] = 0,
        ["ImportDefaultTypeLabel"] = 0,
        ["ImportDefaultCurrencyLabel"] = 0,
        ["ImportTypeExpense"] = 0,
        ["ImportTypeIncome"] = 0,
        ["ImportSignModeLabel"] = 0,
        ["ImportSignModeUnsigned"] = 0,
        ["ImportSignModeNegativeIsExpense"] = 0,
        ["ImportSkipDuplicates"] = 0,
        ["ImportCreateCategories"] = 0,
        ["ImportSummaryFormat"] = 4,
        ["ImportCategoriesToCreate"] = 0,
        ["ImportOnlyProblems"] = 0,
        ["ImportStatusValid"] = 0,
        ["ImportStatusDuplicate"] = 0,
        ["ImportStatusRejected"] = 0,
        ["ImportNewCategoryTag"] = 0,
        ["ImportBackupRecommendation"] = 0,
        ["ImportConfirmButton"] = 0,
        ["ImportCancelButton"] = 0,
        ["ImportCloseButton"] = 0,
        ["ImportChooseAnotherButton"] = 0,
        ["ImportWorkingRead"] = 0,
        ["ImportWorkingPreview"] = 0,
        ["ImportWorkingCommit"] = 0,
        ["ImportDoneFormat"] = 4,
        ["ImportStaleMessage"] = 0,
        ["ImportErrorFailed"] = 0,
        ["ImportErrorUnreadable"] = 0,
        ["ImportErrorMalformedCsv"] = 1,
        ["ImportErrorLegacyXls"] = 0,
        ["ImportErrorBinary"] = 0,
        ["ImportErrorTooLarge"] = 4,
        ["ImportErrorNoRows"] = 0,
        ["ImportErrorPreviewStale"] = 0,
        ["ImportErrorLookupFailed"] = 0,
        ["ImportErrorWriteFailed"] = 0,
        ["ImportErrorMappingIncomplete"] = 0,
        ["ImportReasonColumnCountMismatch"] = 0,
        ["ImportReasonCellError"] = 0,
        ["ImportReasonMissingDate"] = 0,
        ["ImportReasonInvalidDate"] = 0,
        ["ImportReasonDateBefore1900"] = 0,
        ["ImportReasonMissingAmount"] = 0,
        ["ImportReasonInvalidAmount"] = 0,
        ["ImportReasonZeroAmount"] = 0,
        ["ImportReasonNegativeAmount"] = 0,
        ["ImportReasonTooManyDecimals"] = 0,
        ["ImportReasonAmountTooLarge"] = 0,
        ["ImportReasonMissingDescription"] = 0,
        ["ImportReasonDescriptionTooLong"] = 0,
        ["ImportReasonInvalidType"] = 0,
        ["ImportReasonUnsupportedCurrency"] = 0,
        ["ImportReasonAmbiguousCategory"] = 0,
        ["ImportReasonCategoryNameTooLong"] = 0,
        ["ImportReasonTooManyNewCategories"] = 0,
        ["ImportReasonNoFallbackCategory"] = 0,
        ["ImportReasonFutureDate"] = 0,
        ["ImportReasonAmbiguousAmount"] = 0,
        ["ImportReasonTypeDefaulted"] = 0,
        ["ImportReasonCurrencyDefaulted"] = 0,
        ["ImportReasonCategoryFallback"] = 0,
        ["ImportReasonCategoryDropped"] = 0,
        ["ImportReasonPaymentMethodNotFound"] = 0,
        ["ImportReasonDuplicateImported"] = 0,
    };

    private static readonly CultureInfo _es = CultureInfo.GetCultureInfo("es-DO");
    private static readonly CultureInfo _en = CultureInfo.GetCultureInfo("en");

    public static IEnumerable<object[]> Keys() => _expected.Keys.Select(k => new object[] { k });

    public static IEnumerable<object[]> Reasons() => Enum.GetValues<ImportReason>().Select(r => new object[] { r });

    [Fact]
    public void Designer_ExposesEveryExpectedImportKey_AndNothingElseWithThePrefix()
    {
        string[] accessors = typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string) && IsImportKey(p.Name))
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(_expected.Keys.Order(StringComparer.Ordinal), accessors);
    }

    [Fact]
    public void BothResources_DefineTheSameImportKeySet()
    {
        string[] neutral = ImportKeysOf(CultureInfo.InvariantCulture);
        string[] es = ImportKeysOf(_es);

        Assert.Equal(_expected.Keys.Order(StringComparer.Ordinal), neutral);
        Assert.Equal(neutral, es);
    }

    [Fact]
    public void BothResources_DefineTheSameKeySetOverall_AndNoValueIsEmpty()
    {
        string[] neutral = AllKeysOf(CultureInfo.InvariantCulture);
        string[] es = AllKeysOf(_es);

        Assert.Equal(neutral, es);
        foreach (string key in neutral)
        {
            Assert.False(string.IsNullOrWhiteSpace(Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture)), key);
            Assert.False(string.IsNullOrWhiteSpace(Strings.ResourceManager.GetString(key, _es)), key);
        }
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

    [Theory]
    [MemberData(nameof(Reasons))]
    public void EveryImportReason_HasANonEmptyMessage_InEsDoAndEn(ImportReason reason)
    {
        string key = "ImportReason" + reason;

        Assert.False(string.IsNullOrWhiteSpace(Strings.ResourceManager.GetString(key, _es)), key);
        Assert.False(string.IsNullOrWhiteSpace(Strings.ResourceManager.GetString(key, _en)), key);
        Assert.NotNull(typeof(Strings).GetProperty(key, BindingFlags.Public | BindingFlags.Static));
        Assert.Contains(key, _expected.Keys);
    }

    [Fact]
    public void ImportReasonKeys_MapOneToOneToTheEnum()
    {
        string[] fromResx = ImportKeysOf(CultureInfo.InvariantCulture)
            .Where(k => k.StartsWith("ImportReason", StringComparison.Ordinal))
            .Select(k => k["ImportReason".Length..])
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] fromEnum = Enum.GetNames<ImportReason>().Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(fromEnum, fromResx);
    }

    [Fact]
    public void MalformedCsvMessage_HasTheLinePlaceholder_InBothLocales()
    {
        Assert.Contains("{0}", Strings.ResourceManager.GetString("ImportErrorMalformedCsv", CultureInfo.InvariantCulture));
        Assert.Contains("{0}", Strings.ResourceManager.GetString("ImportErrorMalformedCsv", _es));
    }

    [Fact]
    public void Accessors_FollowTheSelectedCulture_AndTheCultureIsRestored()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = _es;
            string spanish = Strings.ImportCloseButton;
            CultureInfo.CurrentUICulture = _en;
            string neutral = Strings.ImportCloseButton;

            Assert.Equal("Cerrar", spanish);
            Assert.Equal("Close", neutral);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static bool IsImportKey(string name) =>
        name.StartsWith("Import", StringComparison.Ordinal) || name == "FileDialogAllSupportedTypes";

    private static string[] ImportKeysOf(CultureInfo culture) => AllKeysOf(culture).Where(IsImportKey).ToArray();

    private static string[] AllKeysOf(CultureInfo culture)
    {
        ResourceSet? set = Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);
        return set!.Cast<DictionaryEntry>()
            .Select(e => (string)e.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
