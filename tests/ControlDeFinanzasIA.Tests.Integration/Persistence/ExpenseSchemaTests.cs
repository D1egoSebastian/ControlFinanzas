using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integración que verifican la forma del schema producido por
/// <see cref="ExpenseConfiguration" /> y la migración <c>AddExpenseTable</c>.
/// </summary>
public sealed class ExpenseSchemaTests
{
    /// <summary>
    /// ST-1: tras aplicar las migraciones, la tabla <c>Expenses</c>
    /// existe en SQLite con las 10 columnas esperadas.
    /// </summary>
    [Fact]
    public async Task Migrate_CreatesExpensesTable()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        List<string> tables = await QueryStringsAsync(
            context,
            "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;");
        Assert.Contains("Expenses", tables);

        List<string> columns = await QueryStringsAsync(
            context,
            "SELECT name FROM pragma_table_info('Expenses') ORDER BY name;");
        Assert.Equal(10, columns.Count);
        Assert.Contains("PaymentMethodId", columns);
    }

    /// <summary>
    /// ST-2: <see cref="Expense" /> tiene las longitudes máximas,
    /// nulabilidad, PK, FK e índices configurados en
    /// <see cref="ExpenseConfiguration" />.
    /// </summary>
    [Fact]
    public async Task ExpenseConfiguration_HasExpectedColumnTypesAndConstraints()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        IEntityType entityType = context.Model.FindEntityType(typeof(Expense))!;
        Assert.Equal("Expenses", entityType.GetTableName());

        IProperty id = entityType.FindProperty(nameof(Expense.Id))!;
        Assert.False(id.ValueGenerated.HasFlag(ValueGenerated.OnAdd));
        Assert.True(id.IsPrimaryKey());

        IProperty description = entityType.FindProperty(nameof(Expense.Description))!;
        Assert.Equal(200, description.GetMaxLength());
        Assert.False(description.IsNullable);

        IProperty note = entityType.FindProperty(nameof(Expense.Note))!;
        Assert.Equal(500, note.GetMaxLength());
        Assert.True(note.IsNullable);

        IEnumerable<IForeignKey> foreignKeys = entityType.GetForeignKeys();
        Assert.Equal(2, foreignKeys.Count());

        IForeignKey categoriesFk = foreignKeys
            .Single(fk => fk.PrincipalEntityType.GetTableName() == "Categories");
        Assert.Equal(DeleteBehavior.Restrict, categoriesFk.DeleteBehavior);

        IForeignKey paymentMethodsFk = foreignKeys
            .Single(fk => fk.PrincipalEntityType.GetTableName() == "PaymentMethods");
        Assert.Equal(DeleteBehavior.SetNull, paymentMethodsFk.DeleteBehavior);

        IProperty paymentMethodId = entityType.FindProperty(nameof(Expense.PaymentMethodId))!;
        Assert.True(paymentMethodId.IsNullable);

        List<string> indexNames = await QueryStringsAsync(
            context,
            "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'Expenses' ORDER BY name;");
        Assert.Contains("IX_Expenses_Date", indexNames);
        Assert.Contains("IX_Expenses_CategoryId", indexNames);
        Assert.Contains("IX_Expenses_PaymentMethodId", indexNames);
    }

    /// <summary>
    /// ST-3: <see cref="Money" /> se descompone en dos columnas
    /// <c>Amount</c> y <c>Currency</c> en la tabla <c>Expenses</c>,
    /// ambas NOT NULL; no existe una columna <c>AmountJson</c> ni una
    /// tabla <c>Money</c>.
    /// </summary>
    [Fact]
    public void Money_OwnsOne_ProducesTwoColumns()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        IEntityType entityType = context.Model.FindEntityType(typeof(Expense))!;
        INavigation amountNavigation = entityType.FindNavigation(nameof(Expense.Amount))!;
        IEntityType moneyType = amountNavigation.TargetEntityType;
        Assert.Equal("Expenses", moneyType.GetTableName());

        IEnumerable<string> propertyNames = moneyType.GetProperties().Select(p => p.Name);
        Assert.Contains("Amount", propertyNames);
        Assert.Contains("Currency", propertyNames);
        Assert.DoesNotContain("AmountJson", propertyNames);

        IProperty amount = moneyType.FindProperty("Amount")!;
        Assert.False(amount.IsNullable);
        Assert.Equal(18, amount.GetPrecision());
        Assert.Equal(4, amount.GetScale());

        IProperty currency = moneyType.FindProperty("Currency")!;
        Assert.False(currency.IsNullable);

        // Nota (Fase 3+): con el overload lambda de OwnsOne
        // (`b.OwnsOne(e => e.Amount, money => ...)`), EF Core 8 NO expone
        // Money como top-level entity en Model.GetEntityTypes() por
        // encapsulacion. El schema real (2 columnas en Expenses) sigue
        // correcto y se verifica arriba via navigation. La lookup por
        // string/typeof queda obsoleta; si se necesita, cambiar a la
        // overload de string en ExpenseConfiguration.
    }

    private static Task<List<string>> QueryStringsAsync(AppDbContext context, string sql)
        => context.Database.SqlQueryRaw<string>(sql).ToListAsync();
}
