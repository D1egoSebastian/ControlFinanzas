using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// The text filter of the Expenses and Income lists against real SQLite: partial, case-insensitive match on the
/// description, and wildcard characters typed by the user are searched literally.
/// </summary>
public sealed class TransactionFilterTextTests
{
    private static readonly DateTime _stamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _categoryId = new("11111111-1111-1111-1111-000000000001");

    private static Expense NewExpense(string description) =>
        Expense.Create(_categoryId, new DateOnly(2026, 1, 15), Money.Create(100m, Currency.DOP), description, null, _stamp);

    private static Income NewIncome(string description) =>
        Income.Create(null, new DateOnly(2026, 1, 15), Money.Create(100m, Currency.DOP), description, null, null, _stamp);

    [Fact]
    public async Task Expenses_TextMatchesPartiallyAndIgnoresAsciiCase()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);
        await repo.AddAsync(NewExpense("Pizza almuerzo"));
        await repo.AddAsync(NewExpense("Gasolina"));
        await repo.AddAsync(NewExpense("PIZZERIA centro"));

        PagedResult<Expense> page = await repo.GetByFilterAsync(new ExpenseFilter { Text = "pizz" });

        Assert.Equal(2, page.Total);
        Assert.All(page.Items, e => Assert.Contains("PIZZ", e.Description, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Expenses_WildcardCharactersAreSearchedLiterally()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);
        await repo.AddAsync(NewExpense("Descuento 50% tienda"));
        await repo.AddAsync(NewExpense("Compra normal"));
        await repo.AddAsync(NewExpense("snake_case nombre"));

        PagedResult<Expense> percent = await repo.GetByFilterAsync(new ExpenseFilter { Text = "50%" });
        PagedResult<Expense> underscore = await repo.GetByFilterAsync(new ExpenseFilter { Text = "e_c" });

        Assert.Equal("Descuento 50% tienda", Assert.Single(percent.Items).Description);
        Assert.Equal("snake_case nombre", Assert.Single(underscore.Items).Description);
    }

    [Fact]
    public async Task Expenses_NullTextAppliesNoFilter()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);
        await repo.AddAsync(NewExpense("Uno"));
        await repo.AddAsync(NewExpense("Dos"));

        PagedResult<Expense> page = await repo.GetByFilterAsync(new ExpenseFilter());

        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task Incomes_TextMatchesPartiallyAndIgnoresAsciiCase_AndWildcardsAreLiteral()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);
        await repo.AddAsync(NewIncome("Salario octubre"));
        await repo.AddAsync(NewIncome("Bono 10% anual"));
        await repo.AddAsync(NewIncome("SALARIO noviembre"));

        PagedResult<Income> salary = await repo.GetByFilterAsync(new IncomeFilter { Text = "salario" });
        PagedResult<Income> percent = await repo.GetByFilterAsync(new IncomeFilter { Text = "10%" });

        Assert.Equal(2, salary.Total);
        Assert.Equal("Bono 10% anual", Assert.Single(percent.Items).Description);
    }
}
