using System.Reflection;
using ControlDeFinanzasIA.Infrastructure.Backup;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>F-019 task 3.1: the catalog mirrors the migrations compiled into Infrastructure.</summary>
public sealed class EfMigrationCatalogTests
{
    [Fact]
    public void KnownMigrationIds_EqualsTheIdsOfTheMigrationClasses()
    {
        string[] expected = typeof(AppDbContext).Assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.GetCustomAttribute<MigrationAttribute>()!.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        var catalog = new EfMigrationCatalog();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, catalog.KnownMigrationIds);
    }

    [Fact]
    public void KnownMigrationIds_IsOrdinalSortedAndContainsTheLatestMigrations()
    {
        var catalog = new EfMigrationCatalog();

        Assert.Equal(catalog.KnownMigrationIds.OrderBy(id => id, StringComparer.Ordinal), catalog.KnownMigrationIds);
        Assert.Contains(catalog.KnownMigrationIds, id => id.EndsWith("_AddNotifications", StringComparison.Ordinal));
        Assert.Contains(catalog.KnownMigrationIds, id => id.EndsWith("_AddSettings", StringComparison.Ordinal));
    }

    [Fact]
    public void Construction_OpensNoConnectionAndCreatesNoFile()
    {
        string[] before = Directory.GetFileSystemEntries(Directory.GetCurrentDirectory());

        _ = new EfMigrationCatalog();

        Assert.Equal(before, Directory.GetFileSystemEntries(Directory.GetCurrentDirectory()));
    }

    [Fact]
    public void KnownMigrationIds_IsTheSameInstanceOnEveryRead()
    {
        var catalog = new EfMigrationCatalog();

        Assert.Same(catalog.KnownMigrationIds, catalog.KnownMigrationIds);
    }
}
