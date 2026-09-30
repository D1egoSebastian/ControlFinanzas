using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Infrastructure.Import;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Tests.Integration.Backup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>Tests of the REAL <see cref="EfImportLookup"/> against a real migrated SQLite file (F-020, S4b).</summary>
public sealed class EfImportLookupTests
{
    private static EfImportLookup CreateLookup(ImportTestDb db, ILogger<EfImportLookup>? logger = null) =>
        new(db.Scopes, logger ?? NullLogger<EfImportLookup>.Instance);

    private static async Task<ImportLookupSnapshot> LoadAsync(EfImportLookup lookup, DateOnly? min, DateOnly? max)
    {
        Result result = await lookup.LoadAsync(min, max);
        Assert.True(result.IsSuccess, result.Error?.Code + " " + result.Error?.Message);
        return (ImportLookupSnapshot)result.Value!;
    }

    [Fact]
    public async Task Load_ReturnsCategoriesAndPaymentMethodsWithSystemFlag()
    {
        using var db = new ImportTestDb();
        Guid custom = db.SeedCategory("Mascotas");
        Guid card = db.SeedPaymentMethod("Tarjeta Gold");

        ImportLookupSnapshot snapshot = await LoadAsync(CreateLookup(db), null, null);

        Assert.Contains(snapshot.Categories, c => c.Id == custom && c.Name == "Mascotas" && !c.IsSystem);
        Assert.Contains(snapshot.Categories, c => c.Name == "Comida" && c.IsSystem);
        Assert.Contains(snapshot.PaymentMethods, p => p.Id == card && p.Name == "Tarjeta Gold" && !p.IsSystem);
        Assert.Contains(snapshot.PaymentMethods, p => p.Name == "Efectivo" && p.IsSystem);
    }

    [Fact]
    public async Task Load_MigratedDatabase_HasSeededFallbackCategoryWithTheImportLimitsId()
    {
        using var db = new ImportTestDb();

        ImportLookupSnapshot snapshot = await LoadAsync(CreateLookup(db), null, null);

        CatalogEntry otros = Assert.Single(snapshot.Categories, c => c.Id == ImportLimits.FallbackCategoryId);
        Assert.Equal("Otros", otros.Name);
        Assert.True(otros.IsSystem);
    }

    [Fact]
    public async Task Load_DateSpan_IncludesBothBoundsAndExcludesOutsideRows()
    {
        using var db = new ImportTestDb();
        Guid category = db.SeedCategory("Cat");
        db.SeedExpense(category, new DateOnly(2026, 3, 4), 1m, Currency.DOP, "before");
        db.SeedExpense(category, new DateOnly(2026, 3, 5), 2m, Currency.DOP, "lower bound");
        db.SeedExpense(category, new DateOnly(2026, 3, 7), 3m, Currency.DOP, "inside");
        db.SeedExpense(category, new DateOnly(2026, 3, 10), 4m, Currency.DOP, "upper bound");
        db.SeedExpense(category, new DateOnly(2026, 3, 11), 5m, Currency.DOP, "after");

        ImportLookupSnapshot snapshot = await LoadAsync(CreateLookup(db), new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 10));

        Assert.Equal(
            new[] { "inside", "lower bound", "upper bound" },
            snapshot.Existing.Select(e => e.Description).OrderBy(d => d, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task Load_ReturnsExpensesAndIncomesWithExactFourDecimalAmounts()
    {
        using var db = new ImportTestDb();
        Guid category = db.SeedCategory("Cat");
        db.SeedExpense(category, new DateOnly(2026, 3, 5), 12.3456m, Currency.USD, "an expense");
        db.SeedIncome(category, new DateOnly(2026, 3, 6), 99999999999999.9999m, Currency.DOP, "an income");
        db.SeedIncome(null, new DateOnly(2026, 3, 6), 0.0001m, Currency.USD, "no category");

        ImportLookupSnapshot snapshot = await LoadAsync(CreateLookup(db), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(3, snapshot.Existing.Count);
        Assert.Contains(snapshot.Existing, e => e is { Type: TransactionType.Expense, Amount: 12.3456m, Currency: Currency.USD, Description: "an expense" } && e.CategoryId == category);
        Assert.Contains(snapshot.Existing, e => e is { Type: TransactionType.Income, Amount: 99999999999999.9999m, Currency: Currency.DOP } && e.CategoryId == category);
        Assert.Contains(snapshot.Existing, e => e is { Type: TransactionType.Income, Amount: 0.0001m, CategoryId: null });
    }

    [Fact]
    public async Task Load_NullBounds_DoNotQueryExistingRows()
    {
        using var db = new ImportTestDb();
        db.SeedExpense(ImportLimits.FallbackCategoryId, new DateOnly(2026, 3, 5), 2m, Currency.DOP, "exists");

        ImportLookupSnapshot noBounds = await LoadAsync(CreateLookup(db), null, null);
        ImportLookupSnapshot onlyMin = await LoadAsync(CreateLookup(db), new DateOnly(2026, 3, 1), null);

        Assert.Empty(noBounds.Existing);
        Assert.Empty(onlyMin.Existing);
        Assert.NotEmpty(noBounds.Categories);
    }

    [Fact]
    public async Task Load_UsesAFreshScope_NeverTheAmbientContext()
    {
        using var db = new ImportTestDb();
        AppDbContext ambient;
        using (IServiceScope scope = db.Scopes.CreateScope())
        {
            ambient = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ambient.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "UnsavedTracked", CreatedAt = ImportTestDb.SeedInstant, UpdatedAt = ImportTestDb.SeedInstant });
        }

        // The ambient context is disposed together with its scope and holds an unsaved Added entity.
        ImportLookupSnapshot snapshot = await LoadAsync(CreateLookup(db), null, null);

        Assert.DoesNotContain(snapshot.Categories, c => c.Name == "UnsavedTracked");
        Assert.Contains(snapshot.Categories, c => c.Id == ImportLimits.FallbackCategoryId);
    }

    [Fact]
    public async Task Load_WhileAnotherContextIsAliveWithTrackedChanges_DoesNotDisturbIt()
    {
        using var db = new ImportTestDb();
        using IServiceScope scope = db.Scopes.CreateScope();
        AppDbContext ambient = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ambient.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "StillPending", CreatedAt = ImportTestDb.SeedInstant, UpdatedAt = ImportTestDb.SeedInstant });

        await LoadAsync(CreateLookup(db), new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Single(ambient.ChangeTracker.Entries<Category>(), e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added);
    }

    [Fact]
    public async Task Load_QueryError_BecomesLookupFailedAndIsLogged()
    {
        using var db = new ImportTestDb(migrate: false);
        var logger = new CapturingLogger<EfImportLookup>();

        Result result = await CreateLookup(db, logger).LoadAsync(null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.LookupFailed, result.Error!.Code);
        Assert.Contains(logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Load_CancelledToken_IsRethrown()
    {
        using var db = new ImportTestDb();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateLookup(db).LoadAsync(null, null, cts.Token));
    }
}
