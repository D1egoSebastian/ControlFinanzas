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
/// <see cref="ExpenseRepository.GetAmountRowsAsync"/> (F-014, D-7). A
/// diferencia de las queries de <c>DashboardAggregateQueryTests</c>, esta
/// es una proyección plana (sin <c>Sum</c>/<c>GroupBy</c>) que alimenta
/// <c>AnomalyDetector</c>: no necesita el workaround de cast a
/// <c>double</c> porque no agrega en SQL.
/// </summary>
public sealed class ExpenseRepositoryAmountRowsTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _comidaCategoryId = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _transporteCategoryId = new("11111111-1111-1111-1111-000000000002");

    [Fact]
    public async Task GetAmountRowsAsync_FiltersByDateRange_ExcludesRowsOutsideRange()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 2, 28), "Fuera antes", 999m, Currency.DOP));
        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 1), "Dentro inicio", 100m, Currency.DOP));
        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 31), "Dentro fin", 200m, Currency.DOP));
        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 4, 1), "Fuera despues", 999m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        IReadOnlyList<ExpenseAmountRow> rows = await repo.GetAmountRowsAsync(period);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.True(r.Date >= period.Start && r.Date <= period.End));
    }

    [Fact]
    public async Task GetAmountRowsAsync_EmptyPeriod_ReturnsEmptyList()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        await repo.AddAsync(CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 5), "Gasto", 100m, Currency.DOP));

        var period = DateRange.Create(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));
        IReadOnlyList<ExpenseAmountRow> rows = await repo.GetAmountRowsAsync(period);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetAmountRowsAsync_OrdersByDateThenById()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Expense later = CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 10), "Segundo", 50m, Currency.DOP);
        Expense earlierA = CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 5), "Primero A", 10m, Currency.DOP);
        Expense earlierB = CreateExpense(_comidaCategoryId, new DateOnly(2026, 3, 5), "Primero B", 20m, Currency.DOP);

        // Insert out of chronological order to prove the query orders,
        // not the insertion order.
        await repo.AddAsync(later);
        await repo.AddAsync(earlierB);
        await repo.AddAsync(earlierA);

        var period = DateRange.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        IReadOnlyList<ExpenseAmountRow> rows = await repo.GetAmountRowsAsync(period);

        Assert.Equal(3, rows.Count);
        Assert.Equal(new DateOnly(2026, 3, 5), rows[0].Date);
        Assert.Equal(new DateOnly(2026, 3, 5), rows[1].Date);
        Assert.Equal(new DateOnly(2026, 3, 10), rows[2].Date);

        // Same-date tie-break is by Id ascending.
        Guid firstOfTie = earlierA.Id.CompareTo(earlierB.Id) <= 0 ? earlierA.Id : earlierB.Id;
        Assert.Equal(firstOfTie, rows[0].ExpenseId);
    }

    [Fact]
    public async Task GetAmountRowsAsync_ProjectsExpectedColumns()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Expense expense = CreateExpense(_transporteCategoryId, new DateOnly(2026, 3, 7), "Taxi aeropuerto", 45.5m, Currency.USD);
        await repo.AddAsync(expense);

        var period = DateRange.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        IReadOnlyList<ExpenseAmountRow> rows = await repo.GetAmountRowsAsync(period);

        ExpenseAmountRow row = Assert.Single(rows);
        Assert.Equal(expense.Id, row.ExpenseId);
        Assert.Equal(_transporteCategoryId, row.CategoryId);
        Assert.Equal(new DateOnly(2026, 3, 7), row.Date);
        Assert.Equal(45.5m, row.Amount);
        Assert.Equal(Currency.USD, row.Currency);
        Assert.Equal("Taxi aeropuerto", row.Description);
    }

    private static Expense CreateExpense(Guid categoryId, DateOnly date, string description, decimal amount, Currency currency)
    {
        return Expense.Create(
            categoryId,
            date,
            Money.Create(amount, currency),
            description,
            null,
            _fixedTimestamp);
    }
}
