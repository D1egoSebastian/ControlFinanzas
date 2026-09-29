using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Exports.Common;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for the Dashboard export commands (F-018, S4): cancel, success, failure mapping,
/// the single-flight guard and resx-backed labels. The real save dialog is not driven here.
/// </summary>
public sealed class DashboardViewModelExportTests
{
    private static readonly DateOnly _today = new(2026, 3, 15);
    private static readonly DateOnly _marchAnchor = new(2026, 3, 1);

    private static (DashboardViewModel ViewModel, ExportFixtures Export) Create()
    {
        FakeExpenseRepository expenses = new();
        FakeIncomeRepository incomes = new();
        var export = ExportFixtures.Create(expenses, incomes);
        DashboardViewModel viewModel = DashboardViewModelTests.CreateViewModel(expenses, incomes, _today, export: export);
        return (viewModel, export);
    }

    private static CultureInfo UseCulture(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        return culture;
    }

    [Fact]
    public async Task ExportPdf_Cancel_NeverCallsUseCase_ShowsNoMessage_AndClearsFlag()
    {
        (DashboardViewModel vm, ExportFixtures export) = Create();
        export.Dialog.Result = null;

        await vm.ExportPdfCommand.ExecuteAsync(null);

        Assert.Equal(1, export.Dialog.CallCount);
        Assert.Equal(0, export.PdfWriter.CallCount);
        Assert.Equal(string.Empty, vm.ExportStatusMessage);
        Assert.False(vm.IsExportError);
        Assert.False(vm.IsExporting);
    }

    [Fact]
    public async Task ExportPdf_Success_WritesPdfForSelectedMonth_AndShowsChosenFileName()
    {
        (DashboardViewModel vm, ExportFixtures export) = Create();
        using SaveTargets.RecordingSaveTarget target = SaveTargets.Recording();
        export.Dialog.Result = target;

        await vm.ExportPdfCommand.ExecuteAsync(null);

        Assert.Equal(ReportFormat.Pdf, export.Dialog.LastFormat);
        Assert.Equal(1, export.PdfWriter.CallCount);
        Assert.Equal(0, export.XlsxWriter.CallCount);
        Assert.Equal(_marchAnchor, export.PdfWriter.CapturedMonthSummary!.MonthAnchor);
        Assert.Equal(1, target.OpenCount);
        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, Strings.ExportSucceededMessage, "recorded.bin"),
            vm.ExportStatusMessage);
        Assert.False(vm.IsExportError);
        Assert.False(vm.IsExporting);
    }

    [Fact]
    public async Task ExportExcel_Success_WritesXlsx()
    {
        (DashboardViewModel vm, ExportFixtures export) = Create();
        using SaveTargets.RecordingSaveTarget target = SaveTargets.Recording();
        export.Dialog.Result = target;

        await vm.ExportExcelCommand.ExecuteAsync(null);

        Assert.Equal(ReportFormat.Xlsx, export.Dialog.LastFormat);
        Assert.Equal(1, export.XlsxWriter.CallCount);
        Assert.Equal(0, export.PdfWriter.CallCount);
        Assert.False(vm.IsExportError);
        Assert.Contains("recorded.bin", vm.ExportStatusMessage);
    }

    [Fact]
    public async Task Export_UsesMonthCapturedBeforeTheDialog()
    {
        (DashboardViewModel vm, ExportFixtures export) = Create();
        using SaveTargets.RecordingSaveTarget target = SaveTargets.Recording();
        var gate = new TaskCompletionSource();
        export.Dialog.Gate = gate.Task;
        export.Dialog.Result = target;

        Task pending = vm.ExportPdfCommand.ExecuteAsync(null);
        vm.SelectedMonth = new DateOnly(2026, 1, 1);
        gate.SetResult();
        await pending;

        Assert.Equal(_marchAnchor, export.PdfWriter.CapturedMonthSummary!.MonthAnchor);
    }

    [Fact]
    public async Task Export_WriterFailure_ShowsRenderFailedMessage_AndCommandIsUsableAgain()
    {
        (DashboardViewModel vm, ExportFixtures export) = Create();
        using SaveTargets.RecordingSaveTarget target = SaveTargets.Recording();
        export.Dialog.Result = target;
        export.PdfWriter.FailWith = new Error("boom", "render failed");

        await vm.ExportPdfCommand.ExecuteAsync(null);

        Assert.True(vm.IsExportError);
        Assert.Equal(Strings.ExportRenderFailedMessage, vm.ExportStatusMessage);
        Assert.False(vm.IsExporting);
        Assert.True(vm.ExportPdfCommand.CanExecute(null));
        Assert.True(vm.ExportExcelCommand.CanExecute(null));
        Assert.Equal(0, target.OpenCount);
    }

    [Fact]
    public async Task Export_SaveIoFailure_ShowsSaveFailedMessage()
    {
        (DashboardViewModel vm, ExportFixtures export) = Create();
        using SaveTargets.RecordingSaveTarget target = SaveTargets.ThrowingOnOpen(new IOException("locked"));
        export.Dialog.Result = target;

        await vm.ExportPdfCommand.ExecuteAsync(null);

        Assert.True(vm.IsExportError);
        Assert.Equal(Strings.ExportSaveFailedMessage, vm.ExportStatusMessage);
    }

    [Fact]
    public async Task Export_UnexpectedException_IsSwallowedAndShowsGenericMessage()
    {
        (DashboardViewModel vm, ExportFixtures export) = Create();
        export.Dialog.ThrowWith = new InvalidOperationException("dialog exploded");

        await vm.ExportExcelCommand.ExecuteAsync(null);

        Assert.True(vm.IsExportError);
        Assert.Equal(Strings.ExportFailedMessage, vm.ExportStatusMessage);
        Assert.False(vm.IsExporting);
    }

    [Fact]
    public async Task Export_OperationCanceled_IsNotAFailure()
    {
        (DashboardViewModel vm, ExportFixtures export) = Create();
        export.Dialog.ThrowWith = new OperationCanceledException();

        await vm.ExportPdfCommand.ExecuteAsync(null);

        Assert.False(vm.IsExportError);
        Assert.Equal(string.Empty, vm.ExportStatusMessage);
        Assert.False(vm.IsExporting);
    }

    [Fact]
    public async Task Export_TwoConcurrentInvocations_OpenExactlyOneDialog_ThenAllowsAnother()
    {
        (DashboardViewModel vm, ExportFixtures export) = Create();
        using SaveTargets.RecordingSaveTarget target = SaveTargets.Recording();
        var gate = new TaskCompletionSource();
        export.Dialog.Gate = gate.Task;
        export.Dialog.Result = target;

        Task first = vm.ExportPdfCommand.ExecuteAsync(null);
        Task second = vm.ExportPdfCommand.ExecuteAsync(null);
        Task third = vm.ExportExcelCommand.ExecuteAsync(null);

        Assert.True(vm.IsExporting);
        Assert.False(vm.ExportPdfCommand.CanExecute(null));
        Assert.False(vm.ExportExcelCommand.CanExecute(null));

        gate.SetResult();
        await Task.WhenAll(first, second, third);

        Assert.Equal(1, export.Dialog.CallCount);
        Assert.False(vm.IsExporting);

        export.Dialog.Gate = null;
        await vm.ExportExcelCommand.ExecuteAsync(null);
        Assert.Equal(2, export.Dialog.CallCount);
    }

    [Theory]
    [InlineData(ReportFormat.Pdf, ".pdf")]
    [InlineData(ReportFormat.Xlsx, ".xlsx")]
    public async Task Export_SuggestedFileName_HasKindMonthAndExtension(ReportFormat format, string extension)
    {
        UseCulture("en");
        (DashboardViewModel vm, ExportFixtures export) = Create();
        export.Dialog.Result = null;

        await (format == ReportFormat.Pdf ? vm.ExportPdfCommand : vm.ExportExcelCommand).ExecuteAsync(null);

        Assert.Equal("month-summary-2026-03" + extension, export.Dialog.LastSuggestedFileName);
    }

    [Fact]
    public async Task Export_SuggestedFileName_IsLocalized()
    {
        UseCulture("es-DO");
        (DashboardViewModel vm, ExportFixtures export) = Create();
        export.Dialog.Result = null;

        await vm.ExportPdfCommand.ExecuteAsync(null);

        Assert.Equal("resumen-mensual-2026-03.pdf", export.Dialog.LastSuggestedFileName);
    }

    [Theory]
    [InlineData("es-DO", "Resumen mensual")]
    [InlineData("en", "Monthly summary")]
    public async Task Export_LabelsComeFromResxAndCurrentCulture(string cultureName, string expectedTitle)
    {
        CultureInfo culture = UseCulture(cultureName);
        (DashboardViewModel vm, ExportFixtures export) = Create();
        using SaveTargets.RecordingSaveTarget target = SaveTargets.Recording();
        export.Dialog.Result = target;

        await vm.ExportPdfCommand.ExecuteAsync(null);

        ReportLabels labels = export.PdfWriter.CapturedLabels!;
        Assert.Equal(culture, labels.Culture);
        Assert.Equal(expectedTitle, labels.ReportMonthSummaryTitle);
        Assert.Equal(Strings.ReportMissingRatesNote, labels.ReportMissingRatesNote);
    }

    [Theory]
    [InlineData(ReportErrorCodes.RenderFailed)]
    [InlineData(ReportErrorCodes.SaveFailed)]
    [InlineData(ReportErrorCodes.DataFailed)]
    [InlineData(ReportErrorCodes.WriterUnavailable)]
    public void ExportErrorMessage_KnownCodes_MapToSpecificMessages(string code)
    {
        string message = DashboardViewModel.ExportErrorMessage(code);

        Assert.NotEqual(Strings.ExportFailedMessage, message);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public void ExportErrorMessage_UnknownOrMissingCode_FallsBackToGenericMessage()
    {
        Assert.Equal(Strings.ExportFailedMessage, DashboardViewModel.ExportErrorMessage("Nope"));
        Assert.Equal(Strings.ExportFailedMessage, DashboardViewModel.ExportErrorMessage(null));
    }
}
