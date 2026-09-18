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
/// Tests de integración contra SQLite real para
/// <see cref="ExpenseRepository.GetDailyTotalsAsync"/> (Fase 9, F-009,
/// slice 1). Mismo harness que <see cref="DashboardAggregateQueryTests"/>:
/// grano una fila por (día, moneda), bordes de rango inclusivos, montos
/// redondeados a 4 decimales y sin <see cref="NotSupportedException"/> en el
/// <c>Sum(decimal)</c> (R-5).
/// </summary>
public sealed class CalendarAggregateQueryTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _comidaCategoryId = new("11111111-1111-1111-1111-000000000001");

    [Fact]
    public async Task GetDailyTotalsAsync_MultiCurrencySameDay_ReturnsOneRowPerCurrency()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(new DateOnly(2026, 3, 10), 100m, Currency.DOP));
        await repo.AddAsync(CreateExpense(new DateOnly(2026, 3, 10), 25m, Currency.USD));

        var period = DateRange.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        IReadOnlyList<DayCurrencyTotal> totals = await repo.GetDailyTotalsAsync(period);

        Assert.Equal(2, totals.Count);
        Assert.Equal(100m, totals.Single(t => t.Date == new DateOnly(2026, 3, 10) && t.Currency == Currency.DOP).Amount);
        Assert.Equal(25m, totals.Single(t => t.Date == new DateOnly(2026, 3, 10) && t.Currency == Currency.USD).Amount);
    }

    [Fact]
    public async Task GetDailyTotalsAsync_DifferentDays_ReturnsOneRowPerDay()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(new DateOnly(2026, 3, 5), 100m, Currency.DOP));
        await repo.AddAsync(CreateExpense(new DateOnly(2026, 3, 6), 200m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        IReadOnlyList<DayCurrencyTotal> totals = await repo.GetDailyTotalsAsync(period);

        Assert.Equal(2, totals.Count);
        Assert.Equal(100m, totals.Single(t => t.Date == new DateOnly(2026, 3, 5)).Amount);
        Assert.Equal(200m, totals.Single(t => t.Date == new DateOnly(2026, 3, 6)).Amount);
    }

    [Fact]
    public async Task GetDailyTotalsAsync_RangeBoundaries_AreInclusive()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(new DateOnly(2026, 2, 28), 999m, Currency.DOP));
        await repo.AddAsync(CreateExpense(new DateOnly(2026, 3, 1), 100m, Currency.DOP));
        await repo.AddAsync(CreateExpense(new DateOnly(2026, 3, 31), 50m, Currency.DOP));
        await repo.AddAsync(CreateExpense(new DateOnly(2026, 4, 1), 999m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        IReadOnlyList<DayCurrencyTotal> totals = await repo.GetDailyTotalsAsync(period);

        Assert.Equal(2, totals.Count);
        Assert.Equal(100m, totals.Single(t => t.Date == new DateOnly(2026, 3, 1)).Amount);
        Assert.Equal(50m, totals.Single(t => t.Date == new DateOnly(2026, 3, 31)).Amount);
    }

    [Fact]
    public async Task GetDailyTotalsAsync_EmptyPeriod_ReturnsEmptyList()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(new DateOnly(2026, 3, 5), 100m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
        IReadOnlyList<DayCurrencyTotal> totals = await repo.GetDailyTotalsAsync(period);

        Assert.Empty(totals);
    }

    [Fact]
    public async Task GetDailyTotalsAsync_ManyFractionalCentsSameDay_SumsExactlyDespiteFloatingPointPath()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        // 0.1 no tiene representación binaria exacta; sumarlo 23 veces con
        // IEEE 754 double deriva a 2.3000000000000003 salvo que se redondee
        // de vuelta a la escala de la moneda tras el Sum(double) en SQL —
        // mismo bug de precisión que el primer intento de Fase 8.
        for (int i = 0; i < 23; i++)
        {
            await repo.AddAsync(CreateExpense(new DateOnly(2026, 4, 15), 0.1m, Currency.DOP));
        }

        var period = DateRange.Create(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
        IReadOnlyList<DayCurrencyTotal> totals = await repo.GetDailyTotalsAsync(period);

        Assert.Equal(2.3m, Assert.Single(totals).Amount);
    }

    private static Expense CreateExpense(DateOnly date, decimal amount, Currency currency)
        => Expense.Create(
            _comidaCategoryId,
            date,
            Money.Create(amount, currency),
            "Gasto de prueba",
            null,
            _fixedTimestamp);
}
