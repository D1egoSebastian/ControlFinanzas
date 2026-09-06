using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integración que verifican el mapeo many-to-many entre
/// <see cref="Expense"/> y <see cref="Tag"/> a través de la tabla
/// <c>ExpenseTags</c>.
/// </summary>
public sealed class ExpenseTagManyToManyTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _comidaCategoryId = new("11111111-1111-1111-1111-000000000001");

    private static Tag CreateTag(string name, string? color = null)
        => Tag.Create(name, color, _fixedTimestamp);

    private static Expense CreateExpense(string description)
        => Expense.Create(
            _comidaCategoryId,
            new DateOnly(2026, 1, 15),
            Money.Create(100m, Currency.DOP),
            description,
            null,
            _fixedTimestamp);

    [Fact]
    public async Task AddExpenseWithTags_PersistsBothAndJoinRows()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        Tag tag1 = CreateTag("TagOne", "#FF0000");
        Tag tag2 = CreateTag("TagTwo", "#00FF00");
        context.Tags.AddRange(tag1, tag2);
        await context.SaveChangesAsync();

        Expense expense = CreateExpense("Dinner");
        expense.AddTag(tag1);
        expense.AddTag(tag2);

        context.Expenses.Add(expense);
        await context.SaveChangesAsync();

        List<ExpenseTagRow> rows = await context.Database
            .SqlQueryRaw<ExpenseTagRow>(
                "SELECT ExpenseId, TagId FROM ExpenseTags WHERE ExpenseId = {0}",
                expense.Id)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.ExpenseId == expense.Id && r.TagId == tag1.Id);
        Assert.Contains(rows, r => r.ExpenseId == expense.Id && r.TagId == tag2.Id);
    }

    [Fact]
    public async Task ReloadExpenseWithTags_PopulatesTagsFromJoinTable()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        Tag tag1 = CreateTag("ReloadOne");
        Tag tag2 = CreateTag("ReloadTwo");
        context.Tags.AddRange(tag1, tag2);
        await context.SaveChangesAsync();

        Expense expense = CreateExpense("Lunch");
        expense.AddTag(tag1);
        expense.AddTag(tag2);
        context.Expenses.Add(expense);
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        Expense reloaded = await context.Expenses
            .AsNoTracking()
            .Include(e => e.Tags)
            .SingleAsync(e => e.Id == expense.Id);

        Assert.Equal(2, reloaded.Tags.Count);
        Assert.Contains(reloaded.Tags, t => t.Id == tag1.Id);
        Assert.Contains(reloaded.Tags, t => t.Id == tag2.Id);
    }

    [Fact]
    public async Task DeleteExpense_CascadesJoinRows()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        Tag tag = CreateTag("CascadeExpense");
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        Expense expense = CreateExpense("Breakfast");
        expense.AddTag(tag);
        context.Expenses.Add(expense);
        await context.SaveChangesAsync();

        int joinRowsBefore = await CountExpenseTagsAsync(context, expense.Id);
        Assert.Equal(1, joinRowsBefore);

        context.Expenses.Remove(expense);
        await context.SaveChangesAsync();

        int joinRowsAfter = await CountExpenseTagsAsync(context, expense.Id);
        Assert.Equal(0, joinRowsAfter);

        Tag? persistedTag = await context.Tags.FindAsync(tag.Id);
        Assert.NotNull(persistedTag);
    }

    [Fact]
    public async Task DeleteTag_CascadesJoinRows()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var tagRepo = new TagRepository(context);

        Tag tagA = CreateTag("CascadeTagA");
        Tag tagB = CreateTag("CascadeTagB");
        context.Tags.AddRange(tagA, tagB);
        await context.SaveChangesAsync();

        Expense expense = CreateExpense("Brunch");
        expense.AddTag(tagA);
        expense.AddTag(tagB);
        context.Expenses.Add(expense);
        await context.SaveChangesAsync();

        int joinRowsBefore = await CountTagJoinRowsAsync(context, tagA.Id);
        Assert.Equal(1, joinRowsBefore);

        Result result = await tagRepo.RemoveAsync(tagA.Id);

        Assert.True(result.IsSuccess);

        int joinRowsAfter = await CountTagJoinRowsAsync(context, tagA.Id);
        Assert.Equal(0, joinRowsAfter);

        Expense? persistedExpense = await context.Expenses.FindAsync(expense.Id);
        Assert.NotNull(persistedExpense);

        Tag? persistedTagB = await context.Tags.FindAsync(tagB.Id);
        Assert.NotNull(persistedTagB);
    }

    private static Task<int> CountExpenseTagsAsync(AppDbContext context, Guid expenseId)
        => context.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM ExpenseTags WHERE ExpenseId = {0}",
                expenseId)
            .FirstAsync();

    private static Task<int> CountTagJoinRowsAsync(AppDbContext context, Guid tagId)
        => context.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM ExpenseTags WHERE TagId = {0}",
                tagId)
            .FirstAsync();

    private sealed class ExpenseTagRow
    {
        public Guid ExpenseId { get; set; }

        public Guid TagId { get; set; }
    }
}
