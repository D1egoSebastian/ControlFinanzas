using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Exports.ExportMonthSummaryReport;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Builds the two export-related <c>DashboardViewModel</c> constructor arguments (F-018, S4)
/// from in-memory fakes, so existing fixtures only need one extra call.
/// </summary>
public sealed class ExportFixtures
{
    private ExportFixtures(
        ExportMonthSummaryReportUseCase useCase,
        FakeFileSaveDialog dialog,
        FakeReportWriter pdfWriter,
        FakeReportWriter xlsxWriter)
    {
        UseCase = useCase;
        Dialog = dialog;
        PdfWriter = pdfWriter;
        XlsxWriter = xlsxWriter;
    }

    public ExportMonthSummaryReportUseCase UseCase { get; }

    public FakeFileSaveDialog Dialog { get; }

    public FakeReportWriter PdfWriter { get; }

    public FakeReportWriter XlsxWriter { get; }

    /// <summary>Creates the fixture over in-memory repositories (empty unless supplied).</summary>
    public static ExportFixtures Create(
        FakeExpenseRepository? expenses = null,
        FakeIncomeRepository? incomes = null,
        FakeCurrencyConverter? converter = null)
    {
        expenses ??= new FakeExpenseRepository();
        incomes ??= new FakeIncomeRepository();
        converter ??= new FakeCurrencyConverter();
        var categories = new FakeCategoryRepository();
        var pdf = new FakeReportWriter(ReportFormat.Pdf);
        var xlsx = new FakeReportWriter(ReportFormat.Xlsx);

        var useCase = new ExportMonthSummaryReportUseCase(
            new GetMonthSummaryQuery(expenses, incomes, converter),
            new GetCategoryBreakdownQuery(expenses, converter),
            new GetBudgetVsActualQuery(new FakeBudgetRepository(), expenses, categories, converter),
            expenses,
            incomes,
            categories,
            converter,
            new IReportWriter[] { pdf, xlsx },
            new FakeClock { Today = new DateOnly(2026, 3, 20) },
            NullLogger<ExportMonthSummaryReportUseCase>.Instance);

        return new ExportFixtures(useCase, new FakeFileSaveDialog(), pdf, xlsx);
    }
}
