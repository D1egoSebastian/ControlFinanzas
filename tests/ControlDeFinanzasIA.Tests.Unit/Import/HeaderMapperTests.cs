using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Import;

/// <summary>Tests for <see cref="HeaderMapper"/> (F-020, R-9, S-9, C-11).</summary>
public sealed class HeaderMapperTests
{
    [Fact]
    public void AutoMap_Phase18Headers_MapsCompletelyAndIgnoresAmountDop()
    {
        HeaderMapping map = HeaderMapper.AutoMap(
            new[] { "Date", "Type", "Description", "Category", "Amount", "Currency", "AmountDOP" });

        Assert.Equal(new ColumnMapping(0, 1, 2, 3, 4, 5, null), map.Mapping);
        Assert.True(map.Mapping.IsComplete);
        Assert.Equal(new[] { 6 }, map.Ignored);
        Assert.Empty(map.Missing);
        Assert.Empty(map.Conflicting);
    }

    [Fact]
    public void AutoMap_SpanishSynonyms_MapCompletely()
    {
        HeaderMapping map = HeaderMapper.AutoMap(
            new[] { "Fecha", "Concepto", "Monto", "Categoría", "Moneda" });

        Assert.Equal(new ColumnMapping(0, null, 1, 3, 2, 4, null), map.Mapping);
        Assert.True(map.Mapping.IsComplete);
    }

    [Theory]
    [InlineData("fecha", "concepto", "monto", "categoria", "moneda")]
    [InlineData("  FECHA ", "CONCEPTO", "MONTO", "CATEGORIA", "MONEDA")]
    [InlineData("Fecha!", "Con-cepto", "Mon_to", "Categoría ", "Mo neda")]
    public void AutoMap_AccentsCaseAndSeparators_AreNormalized(string a, string b, string c, string d, string e)
    {
        HeaderMapping map = HeaderMapper.AutoMap(new[] { a, b, c, d, e });

        Assert.Equal(new ColumnMapping(0, null, 1, 3, 2, 4, null), map.Mapping);
    }

    [Fact]
    public void AutoMap_PaymentMethodSynonyms_Map()
    {
        Assert.Equal(1, HeaderMapper.AutoMap(new[] { "Fecha", "Método de pago" }).Mapping.PaymentMethod);
        Assert.Equal(1, HeaderMapper.AutoMap(new[] { "Fecha", "Payment Method" }).Mapping.PaymentMethod);
        Assert.Equal(1, HeaderMapper.AutoMap(new[] { "Fecha", "Forma de pago" }).Mapping.PaymentMethod);
    }

    [Theory]
    [InlineData("AmountDOPExtra")]
    [InlineData("Dates")]
    [InlineData("Amounts")]
    [InlineData("TotalAmount")]
    public void AutoMap_UsesExactEqualityOnly(string header)
    {
        HeaderMapping map = HeaderMapper.AutoMap(new[] { header });

        Assert.Equal(new ColumnMapping(null, null, null, null, null, null, null), map.Mapping);
    }

    [Fact]
    public void AutoMap_DuplicateDateHeaders_LeavesDateUnmappedAndConflicting()
    {
        HeaderMapping map = HeaderMapper.AutoMap(new[] { "Fecha", "Date", "Description", "Amount" });

        Assert.Null(map.Mapping.Date);
        Assert.Equal(new[] { ImportField.Date }, map.Conflicting);
        Assert.False(map.Mapping.IsComplete);
        Assert.Equal(3, map.Mapping.Amount);
        Assert.Equal(2, map.Mapping.Description);
    }

    [Fact]
    public void AutoMap_UnknownHeaders_AreNotMapped()
    {
        HeaderMapping map = HeaderMapper.AutoMap(new[] { "Date", "Banana", "Description", "Amount", "Notes" });

        Assert.Equal(new ColumnMapping(0, null, 2, null, 3, null, null), map.Mapping);
        Assert.True(map.Mapping.IsComplete);
    }

    [Fact]
    public void AutoMap_UnrecognizedHeaders_MapNothingAndListRequiredFieldsMissing()
    {
        HeaderMapping map = HeaderMapper.AutoMap(new[] { "When", "What", "How much" });

        Assert.Equal(new ColumnMapping(null, null, null, null, null, null, null), map.Mapping);
        Assert.Equal(new[] { ImportField.Date, ImportField.Description, ImportField.Amount }, map.Missing);
        Assert.False(map.Mapping.IsComplete);
    }

    [Fact]
    public void AutoMap_SpanishAmountDop_IsIgnoredNeverAmount()
    {
        HeaderMapping map = HeaderMapper.AutoMap(new[] { "Fecha", "Descripción", "Monto DOP" });

        Assert.Null(map.Mapping.Amount);
        Assert.Equal(new[] { 2 }, map.Ignored);
        Assert.Contains(ImportField.Amount, map.Missing);
    }

    [Fact]
    public void AutoMap_EmptyHeaderList_AllRequiredMissing()
    {
        HeaderMapping map = HeaderMapper.AutoMap(Array.Empty<string>());

        Assert.False(map.Mapping.IsComplete);
        Assert.Equal(3, map.Missing.Count);
    }

    [Fact]
    public void NormalizeHeader_StripsAccentsCaseAndNonAlphanumerics()
    {
        string normalized = HeaderMapper.NormalizeHeader(" Método de pago ");

        Assert.Equal(HeaderMapper.NormalizeHeader("Metodo de pago"), normalized);
        Assert.Equal(12, normalized.Length);
        Assert.Equal("AMOUNTDOP", HeaderMapper.NormalizeHeader("Amount-DOP"));
    }
}
