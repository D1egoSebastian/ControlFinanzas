using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>
/// Tests of the REAL <see cref="ControlDeFinanzasIA.Application.UseCases.Import.CommitImport.CommitImportUseCase"/>
/// with the real pipeline, reader, lookup and writer over a real migrated SQLite file (F-020, S5). Only the clock
/// is a fake.
/// </summary>
public sealed class CommitImportUseCaseTests
{
    private const string _header = "Date,Type,Description,Category,Amount,Currency";

    private const string _boomTrigger =
        "CREATE TRIGGER boom BEFORE INSERT ON Expenses WHEN NEW.Description = 'BOOM' BEGIN SELECT RAISE(ABORT,'forced'); END";

    [Fact]
    public async Task Commit_HappyPath_InsertsTheRowsAndReturnsTheCounts()
    {
        using var rig = new ImportRig();

        ImportCommitResult result = await rig.ImportOkAsync(ImportRig.Csv(
            _header,
            "2026-03-01,Expense,Supermercado,Comida,1234.50,DOP",
            "2026-03-02,Expense,Alimento,Mascotas,15.25,USD",
            "2026-03-03,Income,Salario,,50000,DOP"));

        Assert.Equal(new ImportCommitResult(2, 1, 1, 0, 0), result);
        (List<Expense> expenses, List<Income> incomes, List<Category> categories) = await rig.ReadAllAsync();
        Category pets = Assert.Single(categories, c => c.Name == "Mascotas");
        Assert.False(pets.IsSystem);
        Assert.Equal(ImportLimits.NewCategoryColor, pets.Color);
        Assert.Null(pets.Icon);

        Expense market = Assert.Single(expenses, e => e.Description == "Supermercado");
        Assert.Equal(1234.5m, market.Amount.Amount);
        Assert.Equal(Currency.DOP, market.Amount.Currency);
        Assert.Equal(new DateOnly(2026, 3, 1), market.Date);
        Assert.Null(market.Note);
        Expense feed = Assert.Single(expenses, e => e.Description == "Alimento");
        Assert.Equal(pets.Id, feed.CategoryId);
        Assert.Equal(Currency.USD, feed.Amount.Currency);

        Income salary = Assert.Single(incomes);
        Assert.Null(salary.CategoryId);
        Assert.Null(salary.Source);
        Assert.Null(salary.Note);
    }

    [Fact]
    public async Task Commit_DbChangedAfterPreview_Stale()
    {
        using var rig = new ImportRig();
        byte[] bytes = ImportRig.Csv(_header, "2026-03-01,Expense,Taxi,Comida,10,DOP");
        ImportOptions options = ImportRig.Options();
        ImportPreview preview = await rig.PreviewAsync(bytes, options);
        (long Expenses, long Incomes, long Categories) before = rig.Counts();
        Guid comida = (await rig.ReadAllAsync()).Categories.Single(c => c.Name == "Comida").Id;

        // The same transaction now exists: the plan must change from "import" to "duplicate".
        rig.Db.SeedExpense(comida, new DateOnly(2026, 3, 1), 10m, Currency.DOP, "Taxi");

        Result result = await rig.Commit.ExecuteAsync(ImportRig.Source(bytes), options, preview.PlanHash);

        Assert.Equal(ImportErrorCodes.PreviewStale, result.Error!.Code);
        Assert.Equal((before.Expenses + 1, before.Incomes, before.Categories), rig.Counts());
        Assert.Equal(0, rig.Writer.Calls);
    }

    [Fact]
    public async Task Commit_OptionsDifferentFromThePreviewed_Stale()
    {
        using var rig = new ImportRig();
        byte[] bytes = ImportRig.Csv(_header, "2026-03-01,Expense,Taxi,Mascotas,10,DOP");
        ImportPreview preview = await rig.PreviewAsync(bytes, ImportRig.Options(createCategories: true));
        (long Expenses, long Incomes, long Categories) before = rig.Counts();

        Result result = await rig.Commit.ExecuteAsync(ImportRig.Source(bytes), ImportRig.Options(createCategories: false), preview.PlanHash);

        Assert.Equal(ImportErrorCodes.PreviewStale, result.Error!.Code);
        Assert.Equal(before, rig.Counts());
        Assert.Equal(0, rig.Writer.Calls);
    }

    [Fact]
    public async Task Commit_ReparsesTheRetainedBytes()
    {
        using var rig = new ImportRig();
        byte[] bytes = ImportRig.Csv(_header, "2026-03-01,Expense,Taxi,Comida,10,DOP");
        ImportOptions options = ImportRig.Options();
        ImportPreview preview = await rig.PreviewAsync(bytes, options);
        Assert.Equal(1, rig.Reader.Calls);

        Result result = await rig.Commit.ExecuteAsync(ImportRig.Source(bytes), options, preview.PlanHash);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, rig.Reader.Calls);
    }

    [Fact]
    public async Task Commit_ReimportingTheSameCsv_InsertsZero()
    {
        using var rig = new ImportRig();
        byte[] bytes = ImportRig.Csv(
            _header,
            "2026-03-01,Expense,Supermercado,Comida,1234.50,DOP",
            "2026-03-02,Expense,Alimento,Mascotas,15.25,USD",
            "2026-03-03,Income,Salario,,50000,DOP");
        await rig.ImportOkAsync(bytes);
        (long Expenses, long Incomes, long Categories) afterFirst = rig.Counts();

        ImportCommitResult second = await rig.ImportOkAsync(bytes);

        Assert.Equal(new ImportCommitResult(0, 0, 0, 3, 0), second);
        Assert.Equal(afterFirst, rig.Counts());
    }

    [Fact]
    public async Task Commit_TwoIdenticalRowsInTheFile_BothImportedIntoAnEmptyDatabase()
    {
        using var rig = new ImportRig();
        (long Expenses, long Incomes, long Categories) before = rig.Counts();

        ImportCommitResult result = await rig.ImportOkAsync(ImportRig.Csv(
            _header,
            "2026-03-01,Expense,Cafe,Comida,3,DOP",
            "2026-03-01,Expense,Cafe,Comida,3,DOP"));

        Assert.Equal(2, result.ExpensesInserted);
        Assert.Equal((before.Expenses + 2, before.Incomes, before.Categories), rig.Counts());
    }

    [Fact]
    public async Task Commit_TwoIdenticalRowsWithOneStored_SkipsOnlyOne()
    {
        using var rig = new ImportRig();
        Guid comida = (await rig.ReadAllAsync()).Categories.Single(c => c.Name == "Comida").Id;
        rig.Db.SeedExpense(comida, new DateOnly(2026, 3, 1), 3m, Currency.DOP, "Cafe");

        ImportCommitResult result = await rig.ImportOkAsync(ImportRig.Csv(
            _header,
            "2026-03-01,Expense,Cafe,Comida,3,DOP",
            "2026-03-01,Expense,Cafe,Comida,3,DOP"));

        Assert.Equal(1, result.ExpensesInserted);
        Assert.Equal(1, result.DuplicatesSkipped);
        Assert.Equal(2, (await rig.ReadAllAsync()).Expenses.Count(e => e.Description == "Cafe"));
    }

    [Fact]
    public async Task Commit_SkipDuplicatesOff_ImportsEverything()
    {
        using var rig = new ImportRig();
        Guid comida = (await rig.ReadAllAsync()).Categories.Single(c => c.Name == "Comida").Id;
        rig.Db.SeedExpense(comida, new DateOnly(2026, 3, 1), 3m, Currency.DOP, "Cafe");

        ImportCommitResult result = await rig.ImportOkAsync(
            ImportRig.Csv(_header, "2026-03-01,Expense,Cafe,Comida,3,DOP", "2026-03-01,Expense,Cafe,Comida,3,DOP"),
            ImportRig.Options(skipDuplicates: false));

        Assert.Equal(new ImportCommitResult(2, 0, 0, 0, 0), result);
    }

    [Fact]
    public async Task Commit_AllRowsRejected_SucceedsWithZerosAndNeverTouchesTheWriter()
    {
        using var rig = new ImportRig();
        (long Expenses, long Incomes, long Categories) before = rig.Counts();

        ImportCommitResult result = await rig.ImportOkAsync(ImportRig.Csv(
            _header,
            "2026-03-01,Expense,Bad amount,Comida,abc,DOP",
            "not-a-date,Expense,Bad date,Comida,5,DOP"));

        Assert.Equal(new ImportCommitResult(0, 0, 0, 0, 2), result);
        Assert.Equal(before, rig.Counts());
        Assert.Equal(0, rig.Writer.Calls);
    }

    [Fact]
    public async Task Commit_MappingIncomplete_ReturnsMappingIncomplete()
    {
        using var rig = new ImportRig();
        byte[] bytes = ImportRig.Csv("When,What,How much", "2026-03-01,Taxi,10");

        Result result = await rig.ImportAsync(bytes);

        Assert.Equal(ImportErrorCodes.MappingIncomplete, result.Error!.Code);
        Assert.Equal(0, rig.Writer.Calls);
    }

    [Fact]
    public async Task Commit_WriteFailsOnTheLastRow_LeavesZeroRowsAndZeroCategories()
    {
        using var rig = new ImportRig();
        rig.Db.Execute(_boomTrigger);
        (long Expenses, long Incomes, long Categories) before = rig.Counts();

        Result result = await rig.ImportAsync(ImportRig.Csv(
            _header,
            "2026-03-01,Expense,Uno,Mascotas,10,DOP",
            "2026-03-02,Income,Pago,Viajes,20,DOP",
            "2026-03-03,Expense,Dos,Viajes,30,DOP",
            "2026-03-04,Expense,BOOM,Mascotas,40,DOP"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.WriteFailed, result.Error!.Code);
        Assert.Equal(before, rig.Counts());
        Assert.Equal(1, rig.Writer.Calls);
    }

    [Fact]
    public async Task Commit_CreateCategoriesOff_UnmatchedExpenseUsesOtrosAndUnmatchedIncomeHasNone()
    {
        using var rig = new ImportRig();
        ImportOptions options = ImportRig.Options(createCategories: false);

        ImportCommitResult result = await rig.ImportOkAsync(
            ImportRig.Csv(_header, "2026-03-01,Expense,Uno,Mascotas,10,DOP", "2026-03-02,Income,Dos,Viajes,20,DOP"),
            options);

        Assert.Equal(new ImportCommitResult(1, 1, 0, 0, 0), result);
        (List<Expense> expenses, List<Income> incomes, List<Category> categories) = await rig.ReadAllAsync();
        Assert.Equal(ImportLimits.FallbackCategoryId, Assert.Single(expenses).CategoryId);
        Assert.Null(Assert.Single(incomes).CategoryId);
        Assert.DoesNotContain(categories, c => c.Name is "Mascotas" or "Viajes");
    }

    [Fact]
    public async Task Commit_CreateCategoriesOn_DeduplicatesByFoldAndCapsAtFifty()
    {
        using var rig = new ImportRig();
        var lines = new List<string> { _header, "2026-03-01,Expense,Uno,Mascotas,10,DOP", "2026-03-02,Expense,Dos,MASCOTAS,10,DOP" };
        lines.AddRange(Enumerable.Range(1, 50).Select(i => $"2026-03-03,Expense,Row {i},Cat{i:00},5,DOP"));

        ImportCommitResult result = await rig.ImportOkAsync(ImportRig.Csv([.. lines]));

        Assert.Equal(50, result.CategoriesCreated);
        Assert.Equal(51, result.ExpensesInserted);
        Assert.Equal(1, result.Rejected);
        (List<Expense> expenses, List<Income> _, List<Category> categories) = await rig.ReadAllAsync();
        Assert.Single(categories, c => c.Name == "Mascotas");
        Assert.DoesNotContain(categories, c => c.Name == "Cat50");
        Assert.DoesNotContain(expenses, e => e.Description == "Row 50");
    }

    [Fact]
    public async Task Commit_CancelledBeforeTheWrite_ThrowsAndWritesNothing()
    {
        using var rig = new ImportRig();
        byte[] bytes = ImportRig.Csv(_header, "2026-03-01,Expense,Uno,Mascotas,10,DOP");
        ImportOptions options = ImportRig.Options();
        ImportPreview preview = await rig.PreviewAsync(bytes, options);
        (long Expenses, long Incomes, long Categories) before = rig.Counts();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => rig.Commit.ExecuteAsync(ImportRig.Source(bytes), options, preview.PlanHash, cts.Token));

        Assert.Equal(before, rig.Counts());
        Assert.Equal(0, rig.Writer.Calls);
    }

    [Theory]
    [InlineData("0.0001")]
    [InlineData("12.3456")]
    [InlineData("123456789012.3456")]
    [InlineData("99999999999999.9999")]
    public async Task Commit_FourDecimalAmounts_RoundTripExactly(string amount)
    {
        using var rig = new ImportRig();

        await rig.ImportOkAsync(ImportRig.Csv(_header, $"2026-03-01,Expense,Precision,Comida,{amount},USD"));

        Expense stored = Assert.Single((await rig.ReadAllAsync()).Expenses);
        Assert.Equal(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), stored.Amount.Amount);
        Assert.Equal(Currency.USD, stored.Amount.Currency);

        // And the stored value is recognised as the same transaction on a re-import.
        ImportCommitResult again = await rig.ImportOkAsync(ImportRig.Csv(_header, $"2026-03-01,Expense,Precision,Comida,{amount},USD"));
        Assert.Equal(new ImportCommitResult(0, 0, 0, 1, 0), again);
    }
}
