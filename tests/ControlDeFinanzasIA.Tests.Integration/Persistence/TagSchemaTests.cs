using ControlDeFinanzasIA.Domain.Catalog;
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
/// <see cref="TagConfiguration" />, <see cref="ExpenseConfiguration" /> y
/// la migración <c>AddTagsAndExpenseTags</c>.
/// </summary>
public sealed class TagSchemaTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _comidaCategoryId = new("11111111-1111-1111-1111-000000000001");

    /// <summary>
    /// ST-1: tras aplicar las migraciones, la tabla <c>Tags</c> existe en
    /// SQLite con las 4 columnas esperadas.
    /// </summary>
    [Fact]
    public async Task Tags_Table_HasExpectedColumns()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        List<string> tables = await QueryStringsAsync(
            context,
            "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;");
        Assert.Contains("Tags", tables);

        List<string> columns = await QueryStringsAsync(
            context,
            "SELECT name FROM pragma_table_info('Tags') ORDER BY name;");
        Assert.Equal(4, columns.Count);
        Assert.Contains("Id", columns);
        Assert.Contains("Name", columns);
        Assert.Contains("Color", columns);
        Assert.Contains("CreatedAt", columns);
    }

    /// <summary>
    /// ST-2: <see cref="Tag.Name" /> tiene una longitud máxima de 50, es
    /// requerido y tiene un índice único.
    /// </summary>
    [Fact]
    public async Task Tags_Name_HasUniqueIndex()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        IEntityType entityType = context.Model.FindEntityType(typeof(Tag))!;
        Assert.Equal("Tags", entityType.GetTableName());

        IProperty name = entityType.FindProperty(nameof(Tag.Name))!;
        Assert.Equal(50, name.GetMaxLength());
        Assert.False(name.IsNullable);

        IIndex index = entityType.GetIndexes().Single(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(Tag.Name));
        Assert.True(index.IsUnique);

        context.Tags.Add(Tag.Create("Duplicado", null, _fixedTimestamp));
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        context.Tags.Add(Tag.Create("Duplicado", "#FF0000", _fixedTimestamp));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>
    /// ST-3: la tabla <c>ExpenseTags</c> tiene una PK compuesta
    /// <c>(ExpenseId, TagId)</c>, ambas FKs con cascada y un índice sobre
    /// <c>TagId</c>.
    /// </summary>
    [Fact]
    public async Task ExpenseTags_HasCompositePKAndBothCascadeFKs()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        IEntityType joinEntity = context.Model.GetEntityTypes()
            .Single(e => e.GetTableName() == "ExpenseTags");

        IEnumerable<string> keyProperties = joinEntity.FindPrimaryKey()!.Properties.Select(p => p.Name);
        Assert.Equal(new[] { "ExpenseId", "TagId" }, keyProperties);

        IForeignKey[] foreignKeys = joinEntity.GetForeignKeys().ToArray();
        Assert.Equal(2, foreignKeys.Length);
        Assert.All(foreignKeys, fk => Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior));

        List<string> indexNames = await QueryStringsAsync(
            context,
            "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'ExpenseTags' ORDER BY name;");
        Assert.Contains("IX_ExpenseTags_TagId", indexNames);

        var tag = Tag.Create("Viajes", "#00AAFF", _fixedTimestamp);
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        // Nota: NO llamamos ChangeTracker.Clear() aquí. el tag queda en estado
        // Unchanged y EF Core no lo reinserta cuando agregamos el expense con
        // la misma referencia. Llamar Clear() antes provocaba que EF intentara
        // un segundo INSERT del tag (mismo Name -> UNIQUE constraint violation),
        // y luego el join row tampoco se podía persistir.

        var expense = Expense.Create(
            _comidaCategoryId,
            new DateOnly(2026, 1, 15),
            Money.Create(100m, Currency.DOP),
            "Cena",
            null,
            _fixedTimestamp);
        expense.AddTag(tag);
        context.Expenses.Add(expense);
        await context.SaveChangesAsync();

        int joinRowsBefore = await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS \"Value\" FROM ExpenseTags").FirstAsync();
        Assert.Equal(1, joinRowsBefore);

        context.ChangeTracker.Clear();

        Tag? tagToDelete = await context.Tags.FindAsync(tag.Id);
        Assert.NotNull(tagToDelete);
        context.Tags.Remove(tagToDelete!);
        await context.SaveChangesAsync();

        int joinRowsAfter = await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS \"Value\" FROM ExpenseTags").FirstAsync();
        Assert.Equal(0, joinRowsAfter);
    }

    private static Task<List<string>> QueryStringsAsync(AppDbContext context, string sql)
        => context.Database.SqlQueryRaw<string>(sql).ToListAsync();
}
