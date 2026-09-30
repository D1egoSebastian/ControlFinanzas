using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Domain.Enums;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Import;

/// <summary>Tests for <see cref="ImportReason"/> severity split, <see cref="ImportLimits"/> and the option defaults (F-020, R-20, R-24).</summary>
public sealed class ImportReasonTests
{
    [Fact]
    public void IsRejection_TrueForEveryValueBeforeFutureDate_FalseFromItOn()
    {
        foreach (ImportReason reason in Enum.GetValues<ImportReason>())
        {
            bool expected = reason < ImportReason.FutureDate;
            Assert.Equal(expected, ImportReasons.IsRejection(reason));
        }
    }

    [Fact]
    public void Enum_HasNoDuplicateNamesOrValues()
    {
        string[] names = Enum.GetNames<ImportReason>();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        ImportReason[] values = Enum.GetValues<ImportReason>();
        Assert.Equal(values.Length, values.Distinct().Count());
    }

    [Fact]
    public void Limits_MatchTheDesign()
    {
        Assert.Equal(10_485_760, ImportLimits.MaxFileBytes);
        Assert.Equal(50_000, ImportLimits.MaxDataRows);
        Assert.Equal(50, ImportLimits.MaxColumns);
        Assert.Equal(32_768, ImportLimits.MaxFieldChars);
        Assert.Equal(50, ImportLimits.MaxNewCategories);
        Assert.Equal(50, ImportLimits.PreviewRows);
        Assert.Equal(99_999_999_999_999.9999m, ImportLimits.MaxAmount);
        Assert.Equal(new Guid("11111111-1111-1111-1111-000000000009"), ImportLimits.FallbackCategoryId);
        Assert.Equal("#95A5A6", ImportLimits.NewCategoryColor);
    }

    [Fact]
    public void Options_DefaultsMatchTheDesign()
    {
        var options = new ImportOptions(null, null, DecimalStyle.Auto, ImportDateFormat.DayMonthYear);

        Assert.Equal(TransactionType.Expense, options.DefaultType);
        Assert.Equal(Currency.DOP, options.DefaultCurrency);
        Assert.Equal(SignMode.Unsigned, options.SignMode);
        Assert.True(options.SkipDuplicates);
        Assert.True(options.CreateMissingCategories);
    }

    [Fact]
    public void ColumnMapping_IsCompleteNeedsDateDescriptionAndAmount()
    {
        Assert.True(new ColumnMapping(0, null, 1, null, 2, null, null).IsComplete);
        Assert.False(new ColumnMapping(0, 3, 1, 4, null, 5, 6).IsComplete);
        Assert.False(new ColumnMapping(null, null, 1, null, 2, null, null).IsComplete);
        Assert.False(new ColumnMapping(0, null, null, null, 2, null, null).IsComplete);
    }
}
