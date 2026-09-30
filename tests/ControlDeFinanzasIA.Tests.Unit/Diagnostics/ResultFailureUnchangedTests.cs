using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.Tests.Unit.UI;
using ControlDeFinanzasIA.UI.Diagnostics;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.Tests.Unit.Diagnostics;

/// <summary>
/// F-021 S3 (R-7, S-14): an expected <c>Result.Failure</c> is not an unexpected error. With the REAL hooks and the
/// REAL reporter installed, a validation failure of the Expenses form produces no correlation id, no overlay and no
/// log entry, and the inline validation message is unchanged.
/// </summary>
public sealed class ResultFailureUnchangedTests
{
    [Fact]
    public async Task ExpenseRegisterWithInvalidAmount_NeverReachesTheReporter()
    {
        using var rig = new ReporterRig();
        using IDisposable hooks = GlobalExceptionHooks.Install(rig.Reporter, rig.Clock);
        Guid categoryId = new("11111111-1111-1111-1111-000000000001");
        FakeCategoryRepository categories = new();
        categories.SetCategories([ExpensesViewModelTests.CreateCategory(categoryId, "Comida")]);
        ExpensesViewModel viewModel = ExpensesViewModelTests.CreateViewModel(categoryRepo: categories);
        viewModel.SelectedCategoryId = categoryId;
        viewModel.Amount = 0m;
        viewModel.Description = "Almuerzo";

        await viewModel.RegisterExpenseCommand.ExecuteAsync(null);

        Assert.Contains("Amount", viewModel.ValidationErrors.Keys);
        Assert.Contains("El monto debe ser mayor que 0.", viewModel.ValidationErrors["Amount"]);
        Assert.False(rig.Overlay.IsOpen);
        Assert.Equal(Guid.Empty, rig.Overlay.CorrelationId);
        Assert.Empty(rig.LogFiles);
    }
}
