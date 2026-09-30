using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>
/// The Phase 18 -> Phase 20 contract (F-020, S5, R-21): a workbook written by the REAL
/// <c>ClosedXmlReportWriter</c> imports back into identical transactions, and importing it a second time
/// inserts nothing. Real reader, real lookup, real writer, real SQLite file.
/// </summary>
public sealed class ImportRoundTripTests
{
    public static TheoryData<string> ReportTypes => new() { "expense-list", "month-summary" };

    [Theory]
    [MemberData(nameof(ReportTypes))]
    public async Task ExportedWorkbook_ImportsBackIdenticalAndAReimportInsertsZero(string reportType)
    {
        byte[] bytes = reportType == "expense-list" ? await XlsxFixtures.ExpenseListAsync() : await XlsxFixtures.MonthSummaryAsync();
        IReadOnlyList<ReportTransactionLine> lines = reportType == "expense-list"
            ? XlsxFixtures.Lines.Where(l => l.Kind == ReportTransactionKind.Expense).ToList()
            : XlsxFixtures.Lines;
        using var rig = new ImportRig();

        Result result = await rig.ImportAsync(bytes);

        Assert.True(result.IsSuccess, result.Error?.Code + " " + result.Error?.Message);
        var first = (ImportCommitResult)result.Value!;
        Assert.Equal(lines.Count(l => l.Kind == ReportTransactionKind.Expense), first.ExpensesInserted);
        Assert.Equal(lines.Count(l => l.Kind == ReportTransactionKind.Income), first.IncomesInserted);
        Assert.Equal(0, first.Rejected);

        (List<Expense> expenses, List<Income> incomes, List<Category> categories) = await rig.ReadAllAsync();

        foreach (ReportTransactionLine line in lines)
        {
            if (line.Kind == ReportTransactionKind.Expense)
            {
                Expense stored = Assert.Single(expenses, e => e.Description == line.Description);
                Assert.Equal(line.Date, stored.Date);
                Assert.Equal(line.Amount, stored.Amount.Amount);
                Assert.Equal(line.Currency, stored.Amount.Currency);

                // An empty category means "no category": expenses land on the system fallback.
                string expected = line.CategoryName.Length == 0 ? "Otros" : line.CategoryName;
                Assert.Equal(expected, categories.Single(c => c.Id == stored.CategoryId).Name);
            }
            else
            {
                Income stored = Assert.Single(incomes, i => i.Description == line.Description);
                Assert.Equal(line.Date, stored.Date);
                Assert.Equal(line.Amount, stored.Amount.Amount);
                Assert.Equal(line.Currency, stored.Amount.Currency);
                Assert.Equal(
                    line.CategoryName.Length == 0 ? null : line.CategoryName,
                    stored.CategoryId is Guid id ? categories.Single(c => c.Id == id).Name : null);
            }
        }

        (long Expenses, long Incomes, long Categories) afterFirst = rig.Counts();
        ImportCommitResult second = await rig.ImportOkAsync(bytes);

        Assert.Equal(0, second.ExpensesInserted);
        Assert.Equal(0, second.IncomesInserted);
        Assert.Equal(0, second.CategoriesCreated);
        Assert.Equal(lines.Count, second.DuplicatesSkipped);
        Assert.Equal(afterFirst, rig.Counts());
    }
}
