using System.Reflection;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Application.UseCases.Catalog.CreateTag;
using ControlDeFinanzasIA.Application.UseCases.Catalog.GetPaymentMethods;
using ControlDeFinanzasIA.Application.UseCases.Expenses.GetTransactions;
using ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense;
using ControlDeFinanzasIA.Application.UseCases.Expenses.UpdateExpense;
using ControlDeFinanzasIA.Application.UseCases.Import;
using ControlDeFinanzasIA.Application.UseCases.Import.CommitImport;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using ControlDeFinanzasIA.Application.UseCases.Import.PreviewImport;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.ApplySavedView;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.DeleteSavedView;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.GetSavedViews;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.SaveCurrentFilter;
using ControlDeFinanzasIA.Application.UseCases.Transactions.DeleteTransaction;
using ControlDeFinanzasIA.Infrastructure.ExchangeRates;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Tests of the import hosting in <see cref="ExpensesViewModel"/> (F-020, S6b): the REAL Expenses view-model
/// over the REAL <see cref="ImportViewModel"/> and use cases, with fakes only for ports (reader, lookup,
/// writer, picker, repositories).
/// </summary>
public sealed class ExpensesViewModelImportTests
{
    private static readonly string[] _headers = ["Date", "Type", "Description", "Category", "Amount", "Currency"];
    private static readonly DateOnly _today = new(2026, 9, 30);
    private static readonly DateTime _utcNow = new(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc);

    private static RawRow DataRow(int number, string date, string description, string amount) =>
        new(number, new[] { date, "Expense", description, "Comida", amount, "DOP" }.Select(c => new RawCell(c, RawCellKind.Text)).ToList());

    private static RawTable GoodTable() => new(
        _headers,
        [
            DataRow(2, "2026-03-01", "Uno", "10"),
            DataRow(3, "2026-03-02", "Dos", "20"),
        ],
        new TableSourceInfo(ImportFileKind.Csv, "UTF-8", false, ',', null, string.Empty));

    private static Task Bounded(Task task) => task.WaitAsync(TimeSpan.FromSeconds(5));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5);
        }

        Assert.True(condition(), "Condition was not reached in time.");
    }

    internal sealed class Rig
    {
        public Rig(bool withImport = true)
        {
            var clock = new FakeClock { Today = _today, UtcNow = _utcNow };
            Reader = new FakeTabularReader(GoodTable());
            Dialog = new FakeFileOpenDialog { Result = OpenTargets.Recording([1, 2, 3]).Target };
            var lookup = new FakeImportLookup(() => Result_Success());
            var pipeline = new ImportPipeline(Reader, lookup, clock);
            var preview = new PreviewImportUseCase(pipeline, new RecordingLogger<PreviewImportUseCase>());
            var commit = new CommitImportUseCase(pipeline, new FakeImportWriter(), clock, new RecordingLogger<CommitImportUseCase>());
            Import = new ImportViewModel(preview, commit, Dialog, new RecordingLogger<ImportViewModel>())
            {
                PreviewDebounce = TimeSpan.Zero,
            };

            Categories = new FakeCategoryRepository();
            Expenses = new FakeExpenseRepository();
            ViewModel = CreateExpensesViewModel(clock, Expenses, Categories, withImport ? Import : null);
        }

        public FakeTabularReader Reader { get; }

        public FakeFileOpenDialog Dialog { get; }

        public ImportViewModel Import { get; }

        public FakeCategoryRepository Categories { get; }

        public FakeExpenseRepository Expenses { get; }

        public ExpensesViewModel ViewModel { get; }

        private static ControlDeFinanzasIA.Application.Common.Result Result_Success() =>
            ControlDeFinanzasIA.Application.Common.Result.Success(FakeImportLookup.Snapshot());
    }

    private static ExpensesViewModel CreateExpensesViewModel(
        FakeClock clock,
        FakeExpenseRepository expenseRepo,
        FakeCategoryRepository categoryRepo,
        ImportViewModel? import)
    {
        var incomeRepo = new FakeIncomeRepository();
        var tagRepo = new FakeTagRepository();
        var savedViewRepo = new FakeSavedViewRepository();
        var paymentMethodRepo = new FakePaymentMethodRepository();
        ICurrencyConverter currencyConverter = new CurrencyConverter(
            new StubExchangeRateProvider(
                new ConfigurationBuilder().AddInMemoryCollection().Build(),
                new FakeClock { UtcNow = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc) }),
            new MemoryCache(new MemoryCacheOptions()),
            new FakeClock(),
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            NullLogger<CurrencyConverter>.Instance);

        return new ExpensesViewModel(
            new RegisterExpenseUseCase(expenseRepo, clock, new RegisterExpenseValidator(), NullLogger<RegisterExpenseUseCase>.Instance),
            new GetTransactionsQuery(expenseRepo, NullLogger<GetTransactionsQuery>.Instance),
            new UpdateExpenseUseCase(expenseRepo, clock, new UpdateExpenseValidator(), NullLogger<UpdateExpenseUseCase>.Instance),
            new DeleteTransactionUseCase(expenseRepo, incomeRepo, NullLogger<DeleteTransactionUseCase>.Instance),
            new SaveCurrentFilterUseCase(savedViewRepo, clock, new SaveCurrentFilterValidator(), NullLogger<SaveCurrentFilterUseCase>.Instance),
            new ApplySavedViewUseCase(savedViewRepo, NullLogger<ApplySavedViewUseCase>.Instance),
            new GetSavedViewsUseCase(savedViewRepo),
            new DeleteSavedViewUseCase(savedViewRepo, NullLogger<DeleteSavedViewUseCase>.Instance),
            new CreateTagUseCase(tagRepo, clock, new CreateTagValidator(), NullLogger<CreateTagUseCase>.Instance),
            categoryRepo,
            tagRepo,
            new GetPaymentMethodsQuery(paymentMethodRepo, NullLogger<GetPaymentMethodsQuery>.Instance),
            currencyConverter,
            clock,
            NullLogger<ExpensesViewModel>.Instance,
            new FakeStringLocalizer<Strings>(),
            null,
            null,
            null,
            import);
    }

    // ---- (a) optional parameter, existing fixtures unchanged ----

    [Fact]
    public void WithoutImport_TheOpenCommandCannotExecute_AndImportIsNull()
    {
        var rig = new Rig(withImport: false);

        Assert.Null(rig.ViewModel.Import);
        Assert.False(rig.ViewModel.HasImport);
        Assert.False(rig.ViewModel.OpenImportCommand.CanExecute(null));
        Assert.False(rig.ViewModel.IsImportOpen);
    }

    [Fact]
    public async Task WithoutImport_OpeningIsANoOp()
    {
        var rig = new Rig(withImport: false);

        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));

        Assert.False(rig.ViewModel.IsImportOpen);
        Assert.Equal(0, rig.Dialog.Calls);
    }

    [Fact]
    public void Constructor_KeepsSixteenRequiredAndThreeOptionalParameters_WithImportAppendedLast()
    {
        ParameterInfo[] parameters = typeof(ExpensesViewModel).GetConstructors().Single().GetParameters();

        Assert.Equal(20, parameters.Length);
        Assert.Equal(16, parameters.Count(p => !p.HasDefaultValue));
        Assert.Equal(
            ["CategorizeExpenseUseCase", "ExportExpenseListReportUseCase", "IFileSaveDialog", "ImportViewModel"],
            parameters.Skip(16).Select(p => p.ParameterType.Name));
        ParameterInfo last = parameters[^1];
        Assert.Equal(typeof(ImportViewModel), last.ParameterType);
        Assert.True(last.HasDefaultValue);
        Assert.Null(last.DefaultValue);
    }

    // ---- (b) opening ----

    [Fact]
    public async Task OpenImport_OpensThePanel_AndDrivesTheImportFilePickerOnce()
    {
        var rig = new Rig();
        Assert.True(rig.ViewModel.OpenImportCommand.CanExecute(null));

        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));

        Assert.True(rig.ViewModel.IsImportOpen);
        Assert.Equal(1, rig.Dialog.Calls);
        Assert.Equal(ImportPanelState.Previewed, rig.Import.State);
    }

    [Fact]
    public async Task OpenImport_IsNotExecutableWhileOpen_OrWhileExporting()
    {
        var rig = new Rig();
        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));

        Assert.False(rig.ViewModel.OpenImportCommand.CanExecute(null));

        // A second execute while open must not call the picker again.
        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));
        Assert.Equal(1, rig.Dialog.Calls);

        var other = new Rig();
        other.ViewModel.IsExporting = true;
        Assert.False(other.ViewModel.OpenImportCommand.CanExecute(null));
        other.ViewModel.IsExporting = false;
        Assert.True(other.ViewModel.OpenImportCommand.CanExecute(null));
    }

    [Fact]
    public async Task OpenImport_WhileTheImportIsBusy_IsANoOp()
    {
        var rig = new Rig();
        rig.Dialog.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task first = rig.Import.ChooseFileCommand.ExecuteAsync(null);
        Assert.Equal(ImportPanelState.Loading, rig.Import.State);

        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));

        Assert.Equal(1, rig.Dialog.Calls);
        rig.Dialog.Gate.SetResult();
        await Bounded(first);
    }

    // ---- (c) closing ----

    [Fact]
    public async Task CancelledPicker_ClosesThePanel()
    {
        var rig = new Rig();
        rig.Dialog.Result = null;

        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));

        Assert.False(rig.ViewModel.IsImportOpen);
        Assert.True(rig.ViewModel.OpenImportCommand.CanExecute(null));
    }

    [Fact]
    public async Task Cancel_ClosesThePanel_AndDropsTheRetainedFile()
    {
        var rig = new Rig();
        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));
        Assert.True(rig.Import.HasSource);

        rig.Import.CancelCommand.Execute(null);

        Assert.False(rig.ViewModel.IsImportOpen);
        Assert.False(rig.Import.HasSource);
    }

    [Fact]
    public async Task CloseImport_AfterALoadFailure_ClosesTheIdlePanel()
    {
        var rig = new Rig();
        rig.Dialog.Result = new OpenTarget("bank.csv", _ => throw new IOException("locked"));
        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));
        Assert.True(rig.ViewModel.IsImportOpen);
        Assert.Equal(ImportPanelState.Idle, rig.Import.State);
        Assert.True(rig.Import.IsError);

        Assert.True(rig.ViewModel.CloseImportCommand.CanExecute(null));
        rig.ViewModel.CloseImportCommand.Execute(null);

        Assert.False(rig.ViewModel.IsImportOpen);
    }

    [Fact]
    public async Task CloseImport_WhilePreviewed_CancelsTheSession()
    {
        var rig = new Rig();
        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));

        rig.ViewModel.CloseImportCommand.Execute(null);

        Assert.False(rig.ViewModel.IsImportOpen);
        Assert.False(rig.Import.HasSource);
        Assert.Equal(ImportPanelState.Idle, rig.Import.State);
    }

    [Fact]
    public void CloseImport_WithoutImport_CannotExecute()
    {
        var rig = new Rig(withImport: false);

        Assert.False(rig.ViewModel.CloseImportCommand.CanExecute(null));
    }

    // ---- (d) completion reloads the list ----

    [Fact]
    public async Task Completed_ReloadsCategoriesAndTheListOnce()
    {
        var rig = new Rig();
        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));
        await WaitUntilAsync(() => rig.Categories.GetAllCallCount >= 1 && rig.Expenses.GetByFilterCallCount >= 1);
        int categoriesBefore = rig.Categories.GetAllCallCount;
        int listBefore = rig.Expenses.GetByFilterCallCount;

        await Bounded(rig.Import.ConfirmCommand.ExecuteAsync(null));

        await WaitUntilAsync(() => rig.Categories.GetAllCallCount == categoriesBefore + 1);
        await WaitUntilAsync(() => rig.Expenses.GetByFilterCallCount == listBefore + 1);
        await Task.Delay(50);
        Assert.Equal(categoriesBefore + 1, rig.Categories.GetAllCallCount);
        Assert.Equal(listBefore + 1, rig.Expenses.GetByFilterCallCount);
    }

    [Fact]
    public async Task Reopening_AfterDone_DoesNotReloadTwice()
    {
        var rig = new Rig();
        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));
        await Bounded(rig.Import.ConfirmCommand.ExecuteAsync(null));
        rig.Import.CloseCommand.Execute(null);
        Assert.False(rig.ViewModel.IsImportOpen);
        await Task.Delay(50);
        int categoriesAfterFirst = rig.Categories.GetAllCallCount;
        int listAfterFirst = rig.Expenses.GetByFilterCallCount;
        rig.Dialog.Result = OpenTargets.Recording([1, 2, 3]).Target;

        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));
        await Bounded(rig.Import.ConfirmCommand.ExecuteAsync(null));

        await WaitUntilAsync(() => rig.Categories.GetAllCallCount == categoriesAfterFirst + 1);
        await Task.Delay(50);
        Assert.Equal(categoriesAfterFirst + 1, rig.Categories.GetAllCallCount);
        Assert.Equal(listAfterFirst + 1, rig.Expenses.GetByFilterCallCount);
    }

    // ---- navigation: no retained bytes in the history stack ----

    [Fact]
    public async Task NavigatedFrom_DropsTheRetainedFile_AndClosesThePanel()
    {
        var rig = new Rig();
        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));
        Assert.True(rig.Import.HasSource);

        ((INavigationAware)rig.ViewModel).OnNavigatedFrom();

        Assert.False(rig.Import.HasSource);
        Assert.False(rig.ViewModel.IsImportOpen);
        Assert.Equal(ImportPanelState.Idle, rig.Import.State);
    }

    [Fact]
    public async Task NavigatedFrom_AfterDone_ClearsTheResultPanel()
    {
        var rig = new Rig();
        await Bounded(rig.ViewModel.OpenImportCommand.ExecuteAsync(null));
        await Bounded(rig.Import.ConfirmCommand.ExecuteAsync(null));
        Assert.Equal(ImportPanelState.Done, rig.Import.State);

        ((INavigationAware)rig.ViewModel).OnNavigatedFrom();

        Assert.Equal(ImportPanelState.Idle, rig.Import.State);
        Assert.False(rig.ViewModel.IsImportOpen);
    }

    [Fact]
    public void NavigatedFromAndTo_WithoutImport_DoNotThrow()
    {
        var rig = new Rig(withImport: false);

        ((INavigationAware)rig.ViewModel).OnNavigatedFrom();
        ((INavigationAware)rig.ViewModel).OnNavigatedTo();

        Assert.False(rig.ViewModel.IsImportOpen);
    }
}
