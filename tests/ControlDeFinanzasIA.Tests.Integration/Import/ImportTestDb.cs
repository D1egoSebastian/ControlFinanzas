using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>
/// Fixture (F-020, task 4b.1): a private <c>&lt;temp&gt;\import-&lt;guid&gt;\data.db</c> migrated with the real
/// migrations and a real <see cref="ServiceCollection"/> wired with <c>AddPersistence</c>, so
/// <see cref="IServiceScopeFactory"/> is genuine. The user's real data directory is never touched.
/// </summary>
internal sealed class ImportTestDb : IDisposable
{
    public static readonly DateTime SeedInstant = new(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);

    private readonly ServiceProvider _provider;

    public ImportTestDb(bool migrate = true, IInterceptor? interceptor = null)
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), $"import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(DataDirectory);
        DatabasePath = Path.Combine(DataDirectory, "data.db");

        if (migrate)
        {
            using AppDbContext context = CreateStandaloneContext();
            context.Database.Migrate();
        }

        var services = new ServiceCollection();
        services.AddPersistence(new TestPathResolver(DataDirectory, DatabasePath));
        if (interceptor is not null)
        {
            // AddDbContext does not stack in EF Core 8: swap the registration for one that carries the interceptor.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={DatabasePath}").AddInterceptors(interceptor));
        }

        _provider = services.BuildServiceProvider();
    }

    public string DataDirectory { get; }

    public string DatabasePath { get; }

    public IServiceProvider Services => _provider;

    public IServiceScopeFactory Scopes => _provider.GetRequiredService<IServiceScopeFactory>();

    /// <summary>Seeds one non-system category and returns its id.</summary>
    public Guid SeedCategory(string name)
    {
        var id = Guid.NewGuid();
        Save(db => db.Categories.Add(new Category { Id = id, Name = name, IsSystem = false, CreatedAt = SeedInstant, UpdatedAt = SeedInstant }));
        return id;
    }

    public Guid SeedPaymentMethod(string name)
    {
        var id = Guid.NewGuid();
        Save(db => db.PaymentMethods.Add(new PaymentMethod { Id = id, Name = name, IsSystem = false, CreatedAt = SeedInstant, UpdatedAt = SeedInstant }));
        return id;
    }

    public void SeedExpense(Guid categoryId, DateOnly date, decimal amount, Currency currency, string description) =>
        Save(db => db.Expenses.Add(Expense.Create(categoryId, date, Money.Create(amount, currency), description, null, SeedInstant)));

    public void SeedIncome(Guid? categoryId, DateOnly date, decimal amount, Currency currency, string description) =>
        Save(db => db.Incomes.Add(Income.Create(categoryId, date, Money.Create(amount, currency), description, null, null, SeedInstant)));

    /// <summary>Every user table rendered as sorted row strings, from a fresh read-only connection.</summary>
    public Dictionary<string, List<string>> Snapshot()
    {
        SqliteConnection.ClearAllPools();
        return Backup.BackupTestDb.Snapshot(DatabasePath);
    }

    /// <summary>Row count of one table, read through a fresh connection.</summary>
    public long Count(string table)
    {
        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>Runs one raw SQL statement (used to install a failure trigger).</summary>
    public void Execute(string sql)
    {
        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Lower-case hex SHA-256 of the live database file (pools are released first so the file can be read).</summary>
    public string LiveSha256()
    {
        SqliteConnection.ClearAllPools();
        return Backup.BackupTestDb.Sha256Of(DatabasePath);
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    private void Save(Action<AppDbContext> change)
    {
        using AppDbContext context = CreateStandaloneContext();
        change(context);
        context.SaveChanges();
    }

    private AppDbContext CreateStandaloneContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={DatabasePath};Pooling=False")
            .Options;
        return new AppDbContext(options);
    }

    private sealed class TestPathResolver(string dataDirectory, string databasePath) : IPathResolver
    {
        public string DataDirectory => dataDirectory;

        public string DatabaseFilePath => databasePath;
    }
}

/// <summary>A clock fixed at one instant, for previews that must not depend on the real date.</summary>
internal sealed class ImportTestClock(DateOnly today) : IClock
{
    public DateTime UtcNow => today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

    public DateOnly Today => today;
}
