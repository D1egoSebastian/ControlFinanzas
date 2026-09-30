using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;

namespace ControlDeFinanzasIA.Tests.Unit.Import;

/// <summary>
/// Tests of the REAL <see cref="ImportMaterializer"/> (F-020, S5): the plan is turned into domain entities
/// through the domain factories BEFORE anything is written. The plans come from the real pipeline, with fakes
/// only for the reader, the lookup and the clock.
/// </summary>
public sealed class ImportMaterializerTests
{
    private static readonly string[] _headers = ["Date", "Type", "Description", "Category", "Amount", "Currency"];
    private static readonly DateOnly _today = new(2026, 9, 30);
    private static readonly DateTime _utcNow = new(2026, 9, 30, 15, 45, 0, DateTimeKind.Utc);
    private static readonly Guid _comida = new("11111111-1111-1111-1111-000000000001");

    private static ImportOptions Options(bool createCategories = true) =>
        new(null, null, DecimalStyle.Auto, ImportDateFormat.DayMonthYear, CreateMissingCategories: createCategories);

    private static string[] Row(string date, string description, string amount, string category = "Comida", string type = "Expense", string currency = "DOP") =>
        [date, type, description, category, amount, currency];

    private static async Task<ImportPlan> PlanAsync(ImportLookupSnapshot? snapshot, ImportOptions options, params string[][] rows)
    {
        var table = new RawTable(
            _headers,
            rows.Select((cells, i) => new RawRow(i + 2, cells.Select(c => new RawCell(c, RawCellKind.Text)).ToList())).ToList(),
            new TableSourceInfo(ImportFileKind.Csv, "UTF-8", false, ',', null, string.Empty));
        var pipeline = new ImportPipeline(
            new FakeTabularReader(table),
            new FakeImportLookup(snapshot ?? FakeImportLookup.Snapshot()),
            new FakeClock { Today = _today, UtcNow = _utcNow });
        Result result = await pipeline.BuildAsync(new ImportSource("f.csv", [1]), options, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Code);
        return (ImportPlan)result.Value!;
    }

    [Fact]
    public async Task Build_ValidRows_BuildExpensesAndIncomesThroughTheDomainFactories()
    {
        ImportPlan plan = await PlanAsync(
            null,
            Options(),
            Row("2026-03-01", "  Supermercado  ", "1234.5000", "Comida"),
            Row("2026-03-02", "Salario", "50000", string.Empty, "Income", "USD"));

        ImportBatch batch = ImportMaterializer.Build(plan, _utcNow);

        Expense expense = Assert.Single(batch.Expenses);
        Assert.Equal(_comida, expense.CategoryId);
        Assert.Equal(new DateOnly(2026, 3, 1), expense.Date);
        Assert.Equal(1234.5m, expense.Amount.Amount);
        Assert.Equal(Currency.DOP, expense.Amount.Currency);
        Assert.Equal("Supermercado", expense.Description);
        Assert.Null(expense.Note);
        Assert.Equal(_utcNow, expense.CreatedAt);

        Income income = Assert.Single(batch.Incomes);
        Assert.Null(income.CategoryId);
        Assert.Equal(50000m, income.Amount.Amount);
        Assert.Equal(Currency.USD, income.Amount.Currency);
        Assert.Null(income.Note);
        Assert.Null(income.Source);
        Assert.Empty(batch.NewCategories);
    }

    [Fact]
    public async Task Build_DuplicateAndRejectedRows_AreExcluded()
    {
        ImportLookupSnapshot snapshot = FakeImportLookup.Snapshot(
            existing: [new ExistingTransactionKey(new DateOnly(2026, 3, 1), TransactionType.Expense, 10m, Currency.DOP, "Taxi", _comida)]);
        ImportPlan plan = await PlanAsync(
            snapshot,
            Options(),
            Row("2026-03-01", "Taxi", "10"),
            Row("2026-03-02", "Bad amount", "abc"),
            Row("2026-03-03", "Cine", "20"));

        ImportBatch batch = ImportMaterializer.Build(plan, _utcNow);

        Expense only = Assert.Single(batch.Expenses);
        Assert.Equal("Cine", only.Description);
        Assert.Empty(batch.Incomes);
    }

    [Fact]
    public async Task Build_NewCategory_IsNonSystemGreyAndSharedByEveryRowThatUsesIt()
    {
        ImportPlan plan = await PlanAsync(
            null,
            Options(),
            Row("2026-03-01", "Uno", "10", "  Mascotas  "),
            Row("2026-03-02", "Dos", "20", "mascotas"),
            Row("2026-03-03", "Tres", "30", "Viajes", "Income"));

        ImportBatch batch = ImportMaterializer.Build(plan, _utcNow);

        Assert.Equal(2, batch.NewCategories.Count);
        Category pets = batch.NewCategories[0];
        Assert.Equal("Mascotas", pets.Name);
        Assert.Equal(ImportLimits.NewCategoryColor, pets.Color);
        Assert.Equal("#95A5A6", pets.Color);
        Assert.Null(pets.Icon);
        Assert.False(pets.IsSystem);
        Assert.NotEqual(Guid.Empty, pets.Id);
        Assert.Equal(_utcNow, pets.CreatedAt);
        Assert.Equal(_utcNow, pets.UpdatedAt);
        Assert.Equal(2, batch.NewCategories.Select(c => c.Id).Distinct().Count());

        Assert.Equal(2, batch.Expenses.Count);
        Assert.All(batch.Expenses, e => Assert.Equal(pets.Id, e.CategoryId));
        Assert.Equal(batch.NewCategories[1].Id, Assert.Single(batch.Incomes).CategoryId);
    }

    [Fact]
    public async Task Build_PaymentMethodIdComesOnlyFromTheLookup()
    {
        var card = new Guid("22222222-2222-2222-2222-000000000001");
        ImportLookupSnapshot snapshot = FakeImportLookup.Snapshot(paymentMethods: [new CatalogEntry(card, "Tarjeta", false)]);
        var table = new RawTable(
            [.. _headers, "Payment method"],
            [
                new RawRow(2, new[] { "2026-03-01", "Expense", "Uno", "Comida", "10", "DOP", "tarjeta" }
                    .Select(c => new RawCell(c, RawCellKind.Text)).ToList()),
                new RawRow(3, new[] { "2026-03-02", "Expense", "Dos", "Comida", "10", "DOP", "Desconocido" }
                    .Select(c => new RawCell(c, RawCellKind.Text)).ToList()),
            ],
            new TableSourceInfo(ImportFileKind.Csv, "UTF-8", false, ',', null, string.Empty));
        var pipeline = new ImportPipeline(new FakeTabularReader(table), new FakeImportLookup(snapshot), new FakeClock { Today = _today, UtcNow = _utcNow });
        var plan = (ImportPlan)(await pipeline.BuildAsync(new ImportSource("f.csv", [1]), Options(), CancellationToken.None)).Value!;

        ImportBatch batch = ImportMaterializer.Build(plan, _utcNow);

        Assert.Equal(card, batch.Expenses[0].PaymentMethodId);
        Assert.Null(batch.Expenses[1].PaymentMethodId);
    }

    [Fact]
    public async Task Build_NoValidRows_ReturnsAnEmptyBatch()
    {
        ImportPlan plan = await PlanAsync(null, Options(), Row("2026-03-02", "Bad amount", "abc"));

        ImportBatch batch = ImportMaterializer.Build(plan, _utcNow);

        Assert.Empty(batch.Expenses);
        Assert.Empty(batch.Incomes);
        Assert.Empty(batch.NewCategories);
    }

    [Fact]
    public void Build_ValidRowTheDomainRefuses_ThrowsAndNeverReturnsAPartialBatch()
    {
        var good = new PlannedRow(
            new ParsedRow(2, new DateOnly(2026, 3, 1), TransactionType.Expense, 10m, Currency.DOP, "Ok", null, null, [], false),
            ImportRowStatus.Valid,
            _comida,
            DuplicateDetector.CategoryKey(_comida),
            "Comida",
            false,
            null,
            []);
        PlannedRow bad = good with { Parsed = good.Parsed with { RowNumber = 3, Amount = 0m } };
        ImportPlan plan = ManualPlan(good, bad);

        Assert.ThrowsAny<ArgumentException>(() => ImportMaterializer.Build(plan, _utcNow));
    }

    [Fact]
    public void Build_ValidRowMissingItsFields_ThrowsInvalidOperation()
    {
        var broken = new PlannedRow(
            new ParsedRow(2, null, TransactionType.Expense, 10m, Currency.DOP, "Ok", null, null, [], false),
            ImportRowStatus.Valid,
            _comida,
            DuplicateDetector.CategoryKey(_comida),
            "Comida",
            false,
            null,
            []);

        Assert.Throws<InvalidOperationException>(() => ImportMaterializer.Build(ManualPlan(broken), _utcNow));
    }

    [Fact]
    public void Build_ExpenseWithoutACategory_Throws()
    {
        var noCategory = new PlannedRow(
            new ParsedRow(2, new DateOnly(2026, 3, 1), TransactionType.Expense, 10m, Currency.DOP, "Ok", null, null, [], false),
            ImportRowStatus.Valid,
            null,
            DuplicateDetector.CategoryKey(null),
            null,
            false,
            null,
            []);

        Assert.Throws<InvalidOperationException>(() => ImportMaterializer.Build(ManualPlan(noCategory), _utcNow));
    }

    private static ImportPlan ManualPlan(params PlannedRow[] rows) =>
        new(
            new RawTable(_headers, [], new TableSourceInfo(ImportFileKind.Csv, "UTF-8", false, ',', null, string.Empty)),
            new ColumnMapping(0, 1, 2, 3, 4, 5, null),
            HeaderMapper.AutoMap(_headers),
            [],
            [],
            new DecimalStyleDetection(DecimalStyle.Point, 0, 0, 0),
            DateEvidence.NotApplicable,
            rows.Length,
            rows,
            [],
            "hash");
}
