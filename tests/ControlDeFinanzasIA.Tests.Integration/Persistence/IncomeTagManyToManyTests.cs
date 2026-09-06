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
/// <see cref="Income"/> y <see cref="Tag"/> a través de la tabla
/// <c>IncomeTags</c>.
/// </summary>
public sealed class IncomeTagManyToManyTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _comidaCategoryId = new("11111111-1111-1111-1111-000000000001");

    private static Tag CreateTag(string name, string? color = null)
        => Tag.Create(name, color, _fixedTimestamp);

    private static Income CreateIncome(string description)
        => Income.Create(
            _comidaCategoryId,
            new DateOnly(2026, 1, 15),
            Money.Create(100m, Currency.DOP),
            description,
            null,
            null,
            _fixedTimestamp);

    [Fact]
    public async Task AddIncomeWithTags_PersistsBothAndJoinRows()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        Tag tag1 = CreateTag("TagOne", "#FF0000");
        Tag tag2 = CreateTag("TagTwo", "#00FF00");
        context.Tags.AddRange(tag1, tag2);
        await context.SaveChangesAsync();

        Income income = CreateIncome("Salary");
        income.AddTag(tag1);
        income.AddTag(tag2);

        context.Incomes.Add(income);
        await context.SaveChangesAsync();

        List<IncomeTagRow> rows = await context.Database
            .SqlQueryRaw<IncomeTagRow>(
                "SELECT IncomeId, TagId FROM IncomeTags WHERE IncomeId = {0}",
                income.Id)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.IncomeId == income.Id && r.TagId == tag1.Id);
        Assert.Contains(rows, r => r.IncomeId == income.Id && r.TagId == tag2.Id);
    }

    [Fact]
    public async Task ReloadIncomeWithTags_PopulatesTagsFromJoinTable()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        Tag tag1 = CreateTag("ReloadOne");
        Tag tag2 = CreateTag("ReloadTwo");
        context.Tags.AddRange(tag1, tag2);
        await context.SaveChangesAsync();

        Income income = CreateIncome("Bonus");
        income.AddTag(tag1);
        income.AddTag(tag2);
        context.Incomes.Add(income);
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        Income reloaded = await context.Incomes
            .AsNoTracking()
            .Include(i => i.Tags)
            .SingleAsync(i => i.Id == income.Id);

        Assert.Equal(2, reloaded.Tags.Count);
        Assert.Contains(reloaded.Tags, t => t.Id == tag1.Id);
        Assert.Contains(reloaded.Tags, t => t.Id == tag2.Id);
    }

    [Fact]
    public async Task DeleteIncome_CascadesJoinRows()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        Tag tag = CreateTag("CascadeIncome");
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        Income income = CreateIncome("Freelance");
        income.AddTag(tag);
        context.Incomes.Add(income);
        await context.SaveChangesAsync();

        int joinRowsBefore = await CountIncomeTagsAsync(context, income.Id);
        Assert.Equal(1, joinRowsBefore);

        context.Incomes.Remove(income);
        await context.SaveChangesAsync();

        int joinRowsAfter = await CountIncomeTagsAsync(context, income.Id);
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

        Income income = CreateIncome("Consulting");
        income.AddTag(tagA);
        income.AddTag(tagB);
        context.Incomes.Add(income);
        await context.SaveChangesAsync();

        int joinRowsBefore = await CountTagJoinRowsAsync(context, tagA.Id);
        Assert.Equal(1, joinRowsBefore);

        Result result = await tagRepo.RemoveAsync(tagA.Id);

        Assert.True(result.IsSuccess);

        int joinRowsAfter = await CountTagJoinRowsAsync(context, tagA.Id);
        Assert.Equal(0, joinRowsAfter);

        Income? persistedIncome = await context.Incomes.FindAsync(income.Id);
        Assert.NotNull(persistedIncome);

        Tag? persistedTagB = await context.Tags.FindAsync(tagB.Id);
        Assert.NotNull(persistedTagB);
    }

    private static Task<int> CountIncomeTagsAsync(AppDbContext context, Guid incomeId)
        => context.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM IncomeTags WHERE IncomeId = {0}",
                incomeId)
            .FirstAsync();

    private static Task<int> CountTagJoinRowsAsync(AppDbContext context, Guid tagId)
        => context.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM IncomeTags WHERE TagId = {0}",
                tagId)
            .FirstAsync();

    private sealed class IncomeTagRow
    {
        public Guid IncomeId { get; set; }

        public Guid TagId { get; set; }
    }
}
