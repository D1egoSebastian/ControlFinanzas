using System.Text;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Infrastructure.Reports;

namespace ControlDeFinanzasIA.Tests.Integration.Reports;

/// <summary>
/// Structural tests for <see cref="QuestPdfReportWriter"/> (F-018, slice S2): the writer
/// must produce a valid PDF in both locales and never throw for hostile data.
/// </summary>
public sealed class QuestPdfReportWriterTests
{
    private static async Task<byte[]> RenderAsync(MonthSummaryReport report, ReportLabels labels)
    {
        var writer = new QuestPdfReportWriter();
        using var ms = new MemoryStream();
        Result result = await writer.WriteMonthSummaryAsync(report, labels, ms);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return ms.ToArray();
    }

    private static void AssertValidPdf(byte[] bytes)
    {
        Assert.True(bytes.Length > 1024, $"PDF too small: {bytes.Length} bytes");
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void Format_IsPdf() => Assert.Equal(ReportFormat.Pdf, new QuestPdfReportWriter().Format);

    [Fact]
    public async Task WriteMonthSummary_EsDo_ProducesValidPdf() => AssertValidPdf(await RenderAsync(ReportTestData.Report(), ReportTestData.EsLabels()));

    [Fact]
    public async Task WriteMonthSummary_En_ProducesValidPdf() => AssertValidPdf(await RenderAsync(ReportTestData.Report(), ReportTestData.EnLabels()));

    [Fact]
    public async Task WriteMonthSummary_LabelsAreHonored_LocalesDiffer()
    {
        byte[] es = await RenderAsync(ReportTestData.Report(), ReportTestData.EsLabels());
        byte[] en = await RenderAsync(ReportTestData.Report(), ReportTestData.EnLabels());

        Assert.NotEqual(es, en);
    }

    [Fact]
    public async Task WriteMonthSummary_EmptyMonth_ProducesValidPdf() => AssertValidPdf(await RenderAsync(ReportTestData.Report(empty: true), ReportTestData.EnLabels()));

    [Fact]
    public async Task WriteMonthSummary_MissingRates_ProducesValidPdfDifferentFromComplete()
    {
        byte[] withNote = await RenderAsync(ReportTestData.Report(hasMissingRates: true), ReportTestData.EnLabels());
        byte[] without = await RenderAsync(ReportTestData.Report(hasMissingRates: false), ReportTestData.EnLabels());

        AssertValidPdf(withNote);
        Assert.NotEqual(withNote, without);
    }

    [Fact]
    public async Task WriteMonthSummary_IndeterminateBudgetLines_ProducesValidPdf() => AssertValidPdf(await RenderAsync(ReportTestData.Report(indeterminateBudget: true), ReportTestData.EsLabels()));

    [Theory]
    [InlineData("Café mañana á € 100")]
    [InlineData("Pizza \U0001F355 party \U0001F389 ❤️")]
    [InlineData("مرحبا 你好")]
    public async Task WriteMonthSummary_SpecialGlyphs_DoNotThrow(string description) => AssertValidPdf(await RenderAsync(ReportTestData.Report(description: description), ReportTestData.EsLabels()));

    [Fact]
    public async Task WriteMonthSummary_VeryLongDescriptionAndManyRows_DoNotThrow()
    {
        string longText = new('w', 5000);

        byte[] bytes = await RenderAsync(
            ReportTestData.Report(description: longText, transactionCount: 120),
            ReportTestData.EnLabels());

        AssertValidPdf(bytes);
    }

    [Fact]
    public async Task WriteMonthSummary_CancelledToken_ThrowsOperationCanceled()
    {
        var writer = new QuestPdfReportWriter();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var ms = new MemoryStream();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => writer.WriteMonthSummaryAsync(ReportTestData.Report(), ReportTestData.EnLabels(), ms, cts.Token));
    }

    [Fact]
    public async Task WriteMonthSummary_RenderFailure_ReturnsFailureInsteadOfThrowing()
    {
        var writer = new QuestPdfReportWriter();

        // A non-writable stream makes the library fail at write time.
        using var readOnly = new MemoryStream([], writable: false);
        Result result = await writer.WriteMonthSummaryAsync(ReportTestData.Report(), ReportTestData.EnLabels(), readOnly);

        Assert.False(result.IsSuccess);
        Assert.Equal("ReportRenderFailed", result.Error?.Code);
    }

    private static async Task<byte[]> RenderListAsync(ExpenseListReport report, ReportLabels labels)
    {
        var writer = new QuestPdfReportWriter();
        using var ms = new MemoryStream();
        Result result = await writer.WriteExpenseListAsync(report, labels, ms);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return ms.ToArray();
    }

    internal static ExpenseListReport ListReport(
        int count = 5,
        bool missingRate = false,
        string description = "Supermercado",
        ReportFilterSummary? filter = null)
    {
        var lines = new List<ReportTransactionLine>();
        for (int i = 0; i < count; i++)
        {
            lines.Add(new ReportTransactionLine(
                new DateOnly(2026, 9, 1).AddDays(i % 28),
                ReportTransactionKind.Expense,
                description,
                "Comida",
                100m + i,
                i % 2 == 0 ? Currency.USD : Currency.DOP,
                missingRate && i == 0 ? null : 6000m + i));
        }

        return new ExpenseListReport(
            new DateOnly(2026, 9, 29),
            filter ?? new ReportFilterSummary(null, null, null, 0),
            lines,
            lines.Sum(l => l.AmountDop ?? 0m),
            missingRate);
    }

    [Fact]
    public async Task WriteExpenseList_EsDo_ProducesValidPdf() => AssertValidPdf(await RenderListAsync(ListReport(), ReportTestData.EsLabels()));

    [Fact]
    public async Task WriteExpenseList_En_ProducesValidPdf() => AssertValidPdf(await RenderListAsync(ListReport(), ReportTestData.EnLabels()));

    [Fact]
    public async Task WriteExpenseList_Empty_ProducesValidPdf() => AssertValidPdf(await RenderListAsync(ListReport(count: 0), ReportTestData.EnLabels()));

    [Fact]
    public async Task WriteExpenseList_ManyRows_SpansMultiplePagesWithoutException()
    {
        byte[] small = await RenderListAsync(ListReport(count: 5), ReportTestData.EnLabels());
        byte[] big = await RenderListAsync(ListReport(count: 450), ReportTestData.EnLabels());

        AssertValidPdf(big);
        Assert.True(big.Length > small.Length);
    }

    [Fact]
    public async Task WriteExpenseList_MissingRates_DiffersFromComplete()
    {
        byte[] withNote = await RenderListAsync(ListReport(missingRate: true), ReportTestData.EnLabels());
        byte[] without = await RenderListAsync(ListReport(missingRate: false), ReportTestData.EnLabels());

        AssertValidPdf(withNote);
        Assert.NotEqual(withNote, without);
    }

    [Theory]
    [InlineData("Pizza \U0001F355 party \U0001F389 ❤️")]
    [InlineData("Café mañana á € 100")]
    [InlineData("LONG")]
    public async Task WriteExpenseList_LongDescriptionAndEmoji_DoNotThrow(string description)
    {
        string text = description == "LONG" ? new string('w', 5000) : description;

        AssertValidPdf(await RenderListAsync(ListReport(description: text), ReportTestData.EsLabels()));
    }

    [Theory]
    [MemberData(nameof(FilterVariants))]
    public async Task WriteExpenseList_FilterVariants_ProduceValidPdf(ReportFilterSummary filter) =>
        AssertValidPdf(await RenderListAsync(ListReport(filter: filter), ReportTestData.EnLabels()));

    public static TheoryData<ReportFilterSummary> FilterVariants() => new()
    {
        new ReportFilterSummary(null, null, null, 0),
        new ReportFilterSummary(null, null, "Comida", 0),
        new ReportFilterSummary(null, null, null, 3),
        new ReportFilterSummary(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), "Comida", 2),
        new ReportFilterSummary(new DateOnly(2026, 9, 1), null, null, 0),
    };

    [Fact]
    public async Task WriteExpenseList_FiltersChangeOutput()
    {
        byte[] none = await RenderListAsync(ListReport(), ReportTestData.EnLabels());
        byte[] filtered = await RenderListAsync(
            ListReport(filter: new ReportFilterSummary(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), "Comida", 2)),
            ReportTestData.EnLabels());

        Assert.NotEqual(none, filtered);
    }

    [Fact]
    public async Task WriteExpenseList_CancelledToken_ThrowsOperationCanceled()
    {
        var writer = new QuestPdfReportWriter();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var ms = new MemoryStream();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => writer.WriteExpenseListAsync(ListReport(), ReportTestData.EnLabels(), ms, cts.Token));
    }
}
