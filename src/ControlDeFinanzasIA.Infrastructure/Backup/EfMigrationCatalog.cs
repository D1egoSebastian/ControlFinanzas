using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Backup;

/// <summary>
/// <see cref="IMigrationCatalog"/> backed by the migrations compiled into this assembly (F-019, R-9).
/// The list is read once from EF Core's model metadata, which opens no database connection, and cached.
/// </summary>
public sealed class EfMigrationCatalog : IMigrationCatalog
{
    /// <summary>Builds the catalog from <see cref="AppDbContext"/>'s migrations assembly.</summary>
    public EfMigrationCatalog()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new AppDbContext(options);
        KnownMigrationIds = context.Database.GetMigrations()
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> KnownMigrationIds { get; }
}
