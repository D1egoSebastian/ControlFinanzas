using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integración que verifican la forma del schema producido por
/// <see cref="IncomeConfiguration" /> y la migración <c>AddIncomesTable</c>.
/// </summary>
public sealed class IncomeSchemaTests
{
    /// <summary>
    /// ST-1: tras aplicar las migraciones, la tabla <c>Incomes</c>
    /// existe en SQLite con las 11 columnas esperadas.
    /// </summary>
    [Fact]
    public async Task Migrate_CreatesIncomesTable_WithExpectedColumns()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        List<string> tables = await QueryStringsAsync(
            context,
            "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;");
        Assert.Contains("Incomes", tables);

        List<string> columns = await QueryStringsAsync(
            context,
            "SELECT name FROM pragma_table_info('Incomes') ORDER BY name;");
        Assert.Equal(11, columns.Count);
        Assert.Contains("Id", columns);
        Assert.Contains("CategoryId", columns);
        Assert.Contains("Date", columns);
        Assert.Contains("Amount", columns);
        Assert.Contains("Currency", columns);
        Assert.Contains("Description", columns);
        Assert.Contains("Note", columns);
        Assert.Contains("Source", columns);
        Assert.Contains("PaymentMethodId", columns);
        Assert.Contains("CreatedAt", columns);
        Assert.Contains("UpdatedAt", columns);
    }

    /// <summary>
    /// ST-2: <see cref="Income" /> tiene las longitudes máximas,
    /// nulabilidad, PK, FK, índices y <see cref="Money" /> vía
    /// <c>OwnsOne</c> configurados en <see cref="IncomeConfiguration" />.
    /// </summary>
    [Fact]
    public async Task IncomeConfiguration_HasExpectedColumnTypesAndConstraints()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        IEntityType entityType = context.Model.FindEntityType(typeof(Income))!;
        Assert.Equal("Incomes", entityType.GetTableName());

        IProperty id = entityType.FindProperty(nameof(Income.Id))!;
        Assert.False(id.ValueGenerated.HasFlag(ValueGenerated.OnAdd));
        Assert.True(id.IsPrimaryKey());

        IProperty categoryId = entityType.FindProperty(nameof(Income.CategoryId))!;
        Assert.True(categoryId.IsNullable);

        IProperty description = entityType.FindProperty(nameof(Income.Description))!;
        Assert.Equal(200, description.GetMaxLength());
        Assert.False(description.IsNullable);

        IProperty note = entityType.FindProperty(nameof(Income.Note))!;
        Assert.Equal(500, note.GetMaxLength());
        Assert.True(note.IsNullable);

        IProperty source = entityType.FindProperty(nameof(Income.Source))!;
        Assert.Equal(50, source.GetMaxLength());
        Assert.True(source.IsNullable);

        IEnumerable<IForeignKey> foreignKeys = entityType.GetForeignKeys();
        Assert.Equal(2, foreignKeys.Count());

        IForeignKey categoriesFk = foreignKeys
            .Single(fk => fk.PrincipalEntityType.GetTableName() == "Categories");
        Assert.Equal(DeleteBehavior.SetNull, categoriesFk.DeleteBehavior);

        IForeignKey paymentMethodsFk = foreignKeys
            .Single(fk => fk.PrincipalEntityType.GetTableName() == "PaymentMethods");
        Assert.Equal(DeleteBehavior.SetNull, paymentMethodsFk.DeleteBehavior);

        IProperty paymentMethodId = entityType.FindProperty(nameof(Income.PaymentMethodId))!;
        Assert.True(paymentMethodId.IsNullable);

        INavigation amountNavigation = entityType.FindNavigation(nameof(Income.Amount))!;
        IEntityType moneyType = amountNavigation.TargetEntityType;
        Assert.Equal("Incomes", moneyType.GetTableName());

        IProperty amount = moneyType.FindProperty("Amount")!;
        Assert.False(amount.IsNullable);
        Assert.Equal(18, amount.GetPrecision());
        Assert.Equal(4, amount.GetScale());

        IProperty currency = moneyType.FindProperty("Currency")!;
        Assert.False(currency.IsNullable);

        List<string> indexNames = await QueryStringsAsync(
            context,
            "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'Incomes' ORDER BY name;");
        Assert.Contains("IX_Incomes_Date", indexNames);
        Assert.Contains("IX_Incomes_CategoryId", indexNames);
        Assert.Contains("IX_Incomes_PaymentMethodId", indexNames);
    }

    /// <summary>
    /// ST-3: un <see cref="Income" /> puede persistirse y recuperarse
    /// conservando el <see cref="Money" /> embebido.
    /// </summary>
    [Fact]
    public async Task Income_CanBePersistedAndRetrieved_RoundTrip()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        var income = Income.Create(
            null,
            new DateOnly(2026, 1, 15),
            Money.Create(100m, Currency.DOP),
            "Sueldo",
            null,
            "Transferencia",
            new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        context.Incomes.Add(income);
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        Income? retrieved = await context.Incomes.FindAsync(income.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(income.Id, retrieved!.Id);
        Assert.Null(retrieved.CategoryId);
        Assert.Equal(income.Date, retrieved.Date);
        Assert.Equal(income.Description, retrieved.Description);
        Assert.Equal(income.Note, retrieved.Note);
        Assert.Equal(income.Source, retrieved.Source);
        Assert.Equal(income.Amount.Amount, retrieved.Amount.Amount);
        Assert.Equal(income.Amount.Currency, retrieved.Amount.Currency);
        Assert.Equal(income.CreatedAt, retrieved.CreatedAt);
        Assert.Equal(income.UpdatedAt, retrieved.UpdatedAt);
    }

    private static Task<List<string>> QueryStringsAsync(AppDbContext context, string sql)
        => context.Database.SqlQueryRaw<string>(sql).ToListAsync();
}
