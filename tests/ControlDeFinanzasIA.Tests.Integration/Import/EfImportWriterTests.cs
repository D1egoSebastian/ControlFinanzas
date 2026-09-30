using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Import;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Tests.Integration.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>
/// Tests of the REAL <see cref="EfImportWriter"/> on a real migrated SQLite file (F-020, S5, DD-13): one
/// transaction or nothing, and the shared <see cref="AppDbContext"/> never sees an import entity.
/// </summary>
public sealed class EfImportWriterTests
{
    private static readonly DateTime _now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    private static Category NewCategory(string name) =>
        new() { Id = Guid.NewGuid(), Name = name, Color = ImportLimits.NewCategoryColor, IsSystem = false, CreatedAt = _now, UpdatedAt = _now };

    private static Expense NewExpense(Guid categoryId, string description, decimal amount = 10m) =>
        Expense.Create(categoryId, new DateOnly(2026, 3, 1), Money.Create(amount, Currency.DOP), description, null, _now);

    private static Income NewIncome(Guid? categoryId, string description) =>
        Income.Create(categoryId, new DateOnly(2026, 3, 2), Money.Create(20m, Currency.USD), description, null, null, _now);

    private static ImportBatch MixedBatch(string lastExpenseDescription = "Last")
    {
        Category pets = NewCategory("Mascotas");
        Category trips = NewCategory("Viajes");
        return new ImportBatch(
            [pets, trips],
            [NewExpense(pets.Id, "First"), NewExpense(trips.Id, "Second"), NewExpense(pets.Id, lastExpenseDescription)],
            [NewIncome(trips.Id, "Pay one"), NewIncome(null, "Pay two")]);
    }

    private static EfImportWriter CreateWriter(ImportTestDb db, ILogger<EfImportWriter>? logger = null) =>
        new(db.Scopes, logger ?? new CapturingLogger<EfImportWriter>());

    private static (long Expenses, long Incomes, long Categories) Counts(ImportTestDb db) =>
        (db.Count("Expenses"), db.Count("Incomes"), db.Count("Categories"));

    private const string _boomTrigger =
        "CREATE TRIGGER boom BEFORE INSERT ON Expenses WHEN NEW.Description = 'BOOM' BEGIN SELECT RAISE(ABORT,'forced'); END";

    [Fact]
    public async Task Write_HappyPath_InsertsEverythingWithExactlyOneSave()
    {
        var counter = new SaveCountingInterceptor();
        using var db = new ImportTestDb(interceptor: counter);
        (long expenses, long incomes, long categories) = Counts(db);
        ImportBatch batch = MixedBatch();

        Result result = await CreateWriter(db).WriteAsync(batch);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var written = (ImportCommitResult)result.Value!;
        Assert.Equal(3, written.ExpensesInserted);
        Assert.Equal(2, written.IncomesInserted);
        Assert.Equal(2, written.CategoriesCreated);
        Assert.Equal((expenses + 3, incomes + 2, categories + 2), Counts(db));
        Assert.Equal(1, counter.Saves);
    }

    [Fact]
    public async Task Write_HappyPath_PersistsAmountsAndCategoriesExactly()
    {
        using var db = new ImportTestDb();
        Category pets = NewCategory("Mascotas");
        var batch = new ImportBatch([pets], [NewExpense(pets.Id, "Precio raro", 12.3456m)], []);

        Result result = await CreateWriter(db).WriteAsync(batch);

        Assert.True(result.IsSuccess);
        using AppDbContext read = db.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
        Expense stored = await read.Expenses.AsNoTracking().SingleAsync(e => e.Description == "Precio raro");
        Assert.Equal(12.3456m, stored.Amount.Amount);
        Assert.Equal(pets.Id, stored.CategoryId);
        Category storedCategory = await read.Categories.AsNoTracking().SingleAsync(c => c.Id == pets.Id);
        Assert.Equal("Mascotas", storedCategory.Name);
        Assert.False(storedCategory.IsSystem);
    }

    [Fact]
    public async Task Write_EmptyBatch_SucceedsWithoutSaving()
    {
        var counter = new SaveCountingInterceptor();
        using var db = new ImportTestDb(interceptor: counter);

        Result result = await CreateWriter(db).WriteAsync(new ImportBatch([], [], []));

        Assert.True(result.IsSuccess);
        Assert.Equal(new ImportCommitResult(0, 0, 0, 0, 0), result.Value);
        Assert.Equal(0, counter.Saves);
    }

    [Fact]
    public async Task Write_TriggerFailsOnTheLastExpense_LeavesZeroRowsAndZeroCategories()
    {
        using var db = new ImportTestDb();
        db.Execute(_boomTrigger);
        (long Expenses, long Incomes, long Categories) before = Counts(db);
        var logger = new CapturingLogger<EfImportWriter>();

        Result result = await CreateWriter(db, logger).WriteAsync(MixedBatch("BOOM"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.WriteFailed, result.Error!.Code);
        Assert.Equal(before, Counts(db));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Write_TriggerFailsOnTheFirstExpense_LeavesZeroRows()
    {
        using var db = new ImportTestDb();
        db.Execute(_boomTrigger);
        (long Expenses, long Incomes, long Categories) before = Counts(db);
        Category pets = NewCategory("Mascotas");
        var batch = new ImportBatch([pets], [NewExpense(pets.Id, "BOOM"), NewExpense(pets.Id, "Fine")], [NewIncome(null, "Pay")]);

        Result result = await CreateWriter(db).WriteAsync(batch);

        Assert.Equal(ImportErrorCodes.WriteFailed, result.Error!.Code);
        Assert.Equal(before, Counts(db));
    }

    [Fact]
    public async Task Write_NewCategoryCollidingWithAnExistingOneIgnoringCase_FailsTheWholeBatch()
    {
        using var db = new ImportTestDb();
        Guid existing = db.SeedCategory("Existing");
        (long Expenses, long Incomes, long Categories) before = Counts(db);
        Category fresh = NewCategory("Fresh");
        Category clash = NewCategory("EXISTING");
        var batch = new ImportBatch([fresh, clash], [NewExpense(fresh.Id, "One"), NewExpense(existing, "Two")], [NewIncome(clash.Id, "Pay")]);

        Result result = await CreateWriter(db).WriteAsync(batch);

        Assert.Equal(ImportErrorCodes.WriteFailed, result.Error!.Code);
        Assert.Equal(before, Counts(db));
    }

    [Fact]
    public async Task Write_Failure_DoesNotPoisonTheSharedContext()
    {
        using var db = new ImportTestDb();
        db.Execute(_boomTrigger);
        (long Expenses, long Incomes, long Categories) before = Counts(db);

        // The context the ViewModels share: resolved from the ROOT provider and alive across the failing write.
        AppDbContext shared = db.Services.GetRequiredService<AppDbContext>();
        Assert.NotNull(await shared.Categories.FirstAsync());

        Result result = await CreateWriter(db).WriteAsync(MixedBatch("BOOM"));
        Assert.Equal(ImportErrorCodes.WriteFailed, result.Error!.Code);

        Assert.DoesNotContain(shared.ChangeTracker.Entries(), e => e.Entity is Expense or Income);
        Assert.DoesNotContain(shared.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);

        var unrelated = Guid.NewGuid();
        shared.Categories.Add(new Category { Id = unrelated, Name = "Unrelated", IsSystem = false, CreatedAt = _now, UpdatedAt = _now });
        await shared.SaveChangesAsync();

        Assert.Equal((before.Item1, before.Item2, before.Item3 + 1), Counts(db));
        Assert.Equal(unrelated, (await shared.Categories.AsNoTracking().SingleAsync(c => c.Name == "Unrelated")).Id);
    }

    [Fact]
    public async Task Write_CancelledToken_IsIgnoredOnceSavingStarts()
    {
        using var db = new ImportTestDb();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        (long expenses, long incomes, long categories) = Counts(db);

        Result result = await CreateWriter(db).WriteAsync(MixedBatch(), cts.Token);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal((expenses + 3, incomes + 2, categories + 2), Counts(db));
    }

    [Fact]
    public async Task Write_StagesWithAutoDetectChangesOffOnTheScopedContext()
    {
        var counter = new SaveCountingInterceptor();
        using var db = new ImportTestDb(interceptor: counter);

        Result result = await CreateWriter(db).WriteAsync(MixedBatch());

        Assert.True(result.IsSuccess);
        Assert.Equal([false], counter.AutoDetectAtSave);
    }

    [Fact]
    public async Task Write_RunsOffTheCallingThreadAndReturnsAnIncompleteTaskFirst()
    {
        var counter = new SaveCountingInterceptor();
        using var db = new ImportTestDb(interceptor: counter);
        Category pets = NewCategory("Mascotas");
        ImportBatch batch = new([pets], Enumerable.Range(0, 3000).Select(i => NewExpense(pets.Id, $"Row {i}")).ToList(), []);

        Task<Result> pending = CreateWriter(db).WriteAsync(batch);
        bool wasIncomplete = !pending.IsCompleted;
        Result result = await pending;

        Assert.True(wasIncomplete);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotEqual(Environment.CurrentManagedThreadId, Assert.Single(counter.ThreadsAtSave));
    }

    [Fact]
    public async Task Write_NullBatch_Throws()
    {
        using var db = new ImportTestDb();

        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateWriter(db).WriteAsync(null!));
    }
}
