using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integracion que verifican la forma del schema producido por
/// <see cref="DebtConfiguration" />, <see cref="DebtPaymentConfiguration" />
/// y la migracion <c>AddDebts</c>. Mismo patron que
/// <c>ExpenseSchemaTests</c>.
/// </summary>
public sealed class DebtSchemaTests
{
    [Fact]
    public async Task Migrate_CreatesDebtsAndDebtPaymentsTables()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        List<string> tables = await QueryStringsAsync(
            context,
            "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;");
        Assert.Contains("Debts", tables);
        Assert.Contains("DebtPayments", tables);

        List<string> debtColumns = await QueryStringsAsync(
            context,
            "SELECT name FROM pragma_table_info('Debts') ORDER BY name;");
        Assert.Equal(13, debtColumns.Count);
        Assert.Contains("Description", debtColumns);
        Assert.Contains("OriginalAmount", debtColumns);
        Assert.Contains("OriginalAmountCurrency", debtColumns);
        Assert.Contains("InstallmentAmount", debtColumns);
        Assert.Contains("InstallmentAmountCurrency", debtColumns);
        Assert.Contains("Status", debtColumns);

        List<string> paymentColumns = await QueryStringsAsync(
            context,
            "SELECT name FROM pragma_table_info('DebtPayments') ORDER BY name;");
        Assert.Equal(8, paymentColumns.Count);
        Assert.Contains("DebtId", paymentColumns);
        Assert.Contains("InstallmentNumber", paymentColumns);
        Assert.Contains("ScheduledDate", paymentColumns);
        Assert.Contains("PaidDate", paymentColumns);
        Assert.Contains("Amount", paymentColumns);
        Assert.Contains("AmountCurrency", paymentColumns);
        Assert.Contains("Status", paymentColumns);
    }

    [Fact]
    public async Task DebtConfiguration_HasExpectedConstraintsAndIndexes()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        IEntityType entityType = context.Model.FindEntityType(typeof(Debt))!;
        Assert.Equal("Debts", entityType.GetTableName());

        IProperty id = entityType.FindProperty(nameof(Debt.Id))!;
        Assert.False(id.ValueGenerated.HasFlag(ValueGenerated.OnAdd));
        Assert.True(id.IsPrimaryKey());

        IProperty description = entityType.FindProperty(nameof(Debt.Description))!;
        Assert.Equal(Debt.MaxDescriptionLength, description.GetMaxLength());
        Assert.False(description.IsNullable);

        IProperty counterparty = entityType.FindProperty(nameof(Debt.CounterpartyName))!;
        Assert.Equal(Debt.MaxCounterpartyNameLength, counterparty.GetMaxLength());
        Assert.True(counterparty.IsNullable);

        IProperty note = entityType.FindProperty(nameof(Debt.Note))!;
        Assert.Equal(Debt.MaxNoteLength, note.GetMaxLength());
        Assert.True(note.IsNullable);

        List<string> indexNames = await QueryStringsAsync(
            context,
            "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'Debts' ORDER BY name;");
        Assert.Contains("IX_Debts_Status", indexNames);
    }

    [Fact]
    public async Task DebtPaymentConfiguration_HasForeignKeyAndUniqueInstallmentIndex()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        IEntityType entityType = context.Model.FindEntityType(typeof(DebtPayment))!;
        Assert.Equal("DebtPayments", entityType.GetTableName());

        IEnumerable<IForeignKey> foreignKeys = entityType.GetForeignKeys();
        IForeignKey debtFk = Assert.Single(foreignKeys);
        Assert.Equal("Debts", debtFk.PrincipalEntityType.GetTableName());
        Assert.Equal(DeleteBehavior.Cascade, debtFk.DeleteBehavior);

        List<string> indexNames = await QueryStringsAsync(
            context,
            "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'DebtPayments' ORDER BY name;");
        Assert.Contains("IX_DebtPayments_DebtId", indexNames);
        Assert.Contains("IX_DebtPayments_Status", indexNames);
        Assert.Contains("IX_DebtPayments_DebtId_InstallmentNumber", indexNames);

        IIndex uniqueIndex = entityType.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(DebtPayment.DebtId), nameof(DebtPayment.InstallmentNumber) }));
        Assert.True(uniqueIndex.IsUnique);
    }

    private static Task<List<string>> QueryStringsAsync(AppDbContext context, string sql)
        => context.Database.SqlQueryRaw<string>(sql).ToListAsync();
}
