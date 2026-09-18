using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Aggregates;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integración contra SQLite real para las queries de agregación
/// del Dashboard (Fase 8, F-008, slice 1): <c>GetPeriodTotalsAsync</c>,
/// <c>GetCategoryTotalsAsync</c> y <c>GetMonthlyTotalsAsync</c> de
/// <see cref="ExpenseRepository"/> e <see cref="IncomeRepository"/>. Cubre
/// grano multi-moneda, periodo vacío y que el agrupamiento mensual
/// (<c>Date.Year</c>/<c>Date.Month</c>) traduce a SQL en el proveedor
/// SQLite de EF Core 8, mismo patrón que <c>ExpenseRepositoryTests</c>.
/// </summary>
public sealed class DashboardAggregateQueryTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _comidaCategoryId = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _transporteCategoryId = new("11111111-1111-1111-1111-000000000002");

    // ============================================================
    // ExpenseRepository.GetPeriodTotalsAsync
    // ============================================================

    [Fact]
    public async Task Expense_GetPeriodTotalsAsync_MultiCurrency_ReturnsOneRowPerCurrency()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 5), 100m, Currency.DOP));
        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 10), 200m, Currency.DOP));
        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 15), 50m, Currency.USD));

        var period = DateRange.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        IReadOnlyList<CurrencyTotal> totals = await repo.GetPeriodTotalsAsync(period);

        Assert.Equal(2, totals.Count);
        Assert.Equal(300m, totals.Single(t => t.Currency == Currency.DOP).Amount);
        Assert.Equal(50m, totals.Single(t => t.Currency == Currency.USD).Amount);
    }

    [Fact]
    public async Task Expense_GetPeriodTotalsAsync_EmptyPeriod_ReturnsEmptyList()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 5), 100m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
        IReadOnlyList<CurrencyTotal> totals = await repo.GetPeriodTotalsAsync(period);

        Assert.Empty(totals);
    }

    [Fact]
    public async Task Expense_GetPeriodTotalsAsync_ExcludesRowsOutsideRange()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 2, 28), 999m, Currency.DOP));
        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 1), 100m, Currency.DOP));
        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 31), 100m, Currency.DOP));
        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 4, 1), 999m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        IReadOnlyList<CurrencyTotal> totals = await repo.GetPeriodTotalsAsync(period);

        Assert.Equal(200m, Assert.Single(totals).Amount);
    }

    // ============================================================
    // ExpenseRepository.GetCategoryTotalsAsync
    // ============================================================

    [Fact]
    public async Task Expense_GetCategoryTotalsAsync_MultiCategoryMultiCurrency_ReturnsGrainPerCategoryAndCurrency()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 5), 100m, Currency.DOP));
        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 6), 25m, Currency.USD));
        await repo.AddAsync(CreateExpense(_transporteCategoryId, new DateOnly(2026, 3, 7), 300m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        IReadOnlyList<CategoryCurrencyTotal> totals = await repo.GetCategoryTotalsAsync(period);

        Assert.Equal(3, totals.Count);

        CategoryCurrencyTotal comidaDop = totals.Single(t => t.CategoryId == _comidaCategoryId && t.Currency == Currency.DOP);
        Assert.Equal("Comida", comidaDop.CategoryName);
        Assert.Equal(100m, comidaDop.Amount);

        CategoryCurrencyTotal comidaUsd = totals.Single(t => t.CategoryId == _comidaCategoryId && t.Currency == Currency.USD);
        Assert.Equal("Comida", comidaUsd.CategoryName);
        Assert.Equal(25m, comidaUsd.Amount);

        CategoryCurrencyTotal transporteDop = totals.Single(t => t.CategoryId == _transporteCategoryId);
        Assert.Equal("Transporte", transporteDop.CategoryName);
        Assert.Equal(300m, transporteDop.Amount);
    }

    [Fact]
    public async Task Expense_GetCategoryTotalsAsync_EmptyPeriod_ReturnsEmptyList()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 5), 100m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));
        IReadOnlyList<CategoryCurrencyTotal> totals = await repo.GetCategoryTotalsAsync(period);

        Assert.Empty(totals);
    }

    // ============================================================
    // ExpenseRepository.GetMonthlyTotalsAsync — traducción a SQL
    // ============================================================

    [Fact]
    public async Task Expense_GetMonthlyTotalsAsync_GroupsAcrossMonths_TranslatesToSql()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 1, 10), 100m, Currency.DOP));
        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 1, 20), 50m, Currency.DOP));
        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 2, 5), 30m, Currency.USD));
        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 1), 75m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        // Si e.Date.Year/.Month no tradujera a SQL en el proveedor SQLite,
        // esta llamada lanzaría al intentar traducir la expresión (o EF haría
        // client evaluation, lo cual el propio test detecta comparando el
        // resultado esperado por SQL GROUP BY).
        IReadOnlyList<MonthCurrencyTotal> totals = await repo.GetMonthlyTotalsAsync(period);

        Assert.Equal(3, totals.Count);

        MonthCurrencyTotal january = totals.Single(t => t.Year == 2026 && t.Month == 1 && t.Currency == Currency.DOP);
        Assert.Equal(150m, january.Amount);

        MonthCurrencyTotal february = totals.Single(t => t.Year == 2026 && t.Month == 2 && t.Currency == Currency.USD);
        Assert.Equal(30m, february.Amount);

        MonthCurrencyTotal march = totals.Single(t => t.Year == 2026 && t.Month == 3 && t.Currency == Currency.DOP);
        Assert.Equal(75m, march.Amount);
    }

    [Fact]
    public async Task Expense_GetMonthlyTotalsAsync_EmptyPeriod_ReturnsEmptyList()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 1, 10), 100m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2027, 1, 1), new DateOnly(2027, 6, 30));
        IReadOnlyList<MonthCurrencyTotal> totals = await repo.GetMonthlyTotalsAsync(period);

        Assert.Empty(totals);
    }

    // ============================================================
    // IncomeRepository.GetPeriodTotalsAsync / GetMonthlyTotalsAsync
    // ============================================================

    [Fact]
    public async Task Income_GetPeriodTotalsAsync_MultiCurrency_ReturnsOneRowPerCurrency()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        await repo.AddAsync(CreateIncome(new DateOnly(2026, 3, 5), 1000m, Currency.DOP));
        await repo.AddAsync(CreateIncome(new DateOnly(2026, 3, 10), 500m, Currency.DOP));
        await repo.AddAsync(CreateIncome(new DateOnly(2026, 3, 15), 200m, Currency.USD));

        var period = DateRange.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        IReadOnlyList<CurrencyTotal> totals = await repo.GetPeriodTotalsAsync(period);

        Assert.Equal(2, totals.Count);
        Assert.Equal(1500m, totals.Single(t => t.Currency == Currency.DOP).Amount);
        Assert.Equal(200m, totals.Single(t => t.Currency == Currency.USD).Amount);
    }

    [Fact]
    public async Task Income_GetPeriodTotalsAsync_EmptyPeriod_ReturnsEmptyList()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        await repo.AddAsync(CreateIncome(new DateOnly(2026, 3, 5), 1000m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
        IReadOnlyList<CurrencyTotal> totals = await repo.GetPeriodTotalsAsync(period);

        Assert.Empty(totals);
    }

    [Fact]
    public async Task Income_GetMonthlyTotalsAsync_GroupsAcrossMonths_TranslatesToSql()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        await repo.AddAsync(CreateIncome(new DateOnly(2026, 1, 10), 1000m, Currency.DOP));
        await repo.AddAsync(CreateIncome(new DateOnly(2026, 2, 5), 300m, Currency.USD));

        var period = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28));
        IReadOnlyList<MonthCurrencyTotal> totals = await repo.GetMonthlyTotalsAsync(period);

        Assert.Equal(2, totals.Count);
        Assert.Equal(1000m, totals.Single(t => t.Year == 2026 && t.Month == 1).Amount);
        Assert.Equal(300m, totals.Single(t => t.Year == 2026 && t.Month == 2).Amount);
    }

    [Fact]
    public async Task Income_GetMonthlyTotalsAsync_EmptyPeriod_ReturnsEmptyList()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        await repo.AddAsync(CreateIncome(new DateOnly(2026, 1, 10), 1000m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2027, 1, 1), new DateOnly(2027, 6, 30));
        IReadOnlyList<MonthCurrencyTotal> totals = await repo.GetMonthlyTotalsAsync(period);

        Assert.Empty(totals);
    }

    // ============================================================
    // Decimal precision (SQLite Sum(decimal) is unsupported; the
    // repositories cast through double, then round back to Money's
    // scale of 4 to strip binary floating-point noise — see the
    // <remarks> on ExpenseRepository.GetPeriodTotalsAsync)
    // ============================================================

    [Fact]
    public async Task Expense_GetPeriodTotalsAsync_ManyFractionalCents_SumsExactlyDespiteFloatingPointPath()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        // 0.1 has no exact binary representation; summing it 23 times in
        // IEEE 754 double drifts to 2.3000000000000003 unless rounded back
        // to the currency's own scale after the SQL-side Sum(double).
        for (int i = 0; i < 23; i++)
        {
            await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 4, 1 + (i % 28)), 0.1m, Currency.DOP));
        }

        var period = DateRange.Create(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
        IReadOnlyList<CurrencyTotal> totals = await repo.GetPeriodTotalsAsync(period);

        Assert.Equal(2.3m, Assert.Single(totals).Amount);
    }

    [Fact]
    public async Task Expense_GetCategoryTotalsAsync_SumMatchesGetPeriodTotalsAsync_ForSameData()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 5, 3), 19.99m, Currency.DOP));
        await repo.AddAsync(CreateExpense(_transporteCategoryId, new DateOnly(2026, 5, 7), 33.33m, Currency.DOP));
        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 5, 12), 0.01m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));
        IReadOnlyList<CurrencyTotal> periodTotals = await repo.GetPeriodTotalsAsync(period);
        IReadOnlyList<CategoryCurrencyTotal> categoryTotals = await repo.GetCategoryTotalsAsync(period);

        // R-4: category slices must reconcile exactly with the period total.
        // Both are computed independently through the same double-cast Sum
        // path, so this catches float drift the individual tests above
        // could miss if the two paths ever diverged.
        decimal periodTotal = periodTotals.Single(t => t.Currency == Currency.DOP).Amount;
        decimal categorySum = categoryTotals.Where(t => t.Currency == Currency.DOP).Sum(t => t.Amount);
        Assert.Equal(periodTotal, categorySum);
        Assert.Equal(53.33m, periodTotal);
    }

    private static Expense CreateExpense(Guid categoryId, DateOnly date, decimal amount, Currency currency)
    {
        return Expense.Create(
            categoryId,
            date,
            Money.Create(amount, currency),
            "Gasto de prueba",
            null,
            _fixedTimestamp);
    }

    private static Income CreateIncome(DateOnly date, decimal amount, Currency currency)
    {
        return Income.Create(
            null,
            date,
            Money.Create(amount, currency),
            "Ingreso de prueba",
            null,
            null,
            _fixedTimestamp);
    }
}
