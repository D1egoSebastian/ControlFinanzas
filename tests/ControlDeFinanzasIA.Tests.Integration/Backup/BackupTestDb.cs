using System.Globalization;
using System.Security.Cryptography;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.Settings;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Domain.Views;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// Fixture (F-019, task 2.2): a private <c>&lt;temp&gt;\backup-&lt;guid&gt;\</c> data directory holding a
/// real migrated <c>data.db</c> seeded across every table, including a <c>Settings</c> row with the AI
/// API key. The user's real data directory is never touched.
/// </summary>
public sealed class BackupTestDb : IDisposable
{
    /// <summary>The sentinel API key stored in the seeded <c>Settings</c> table.</summary>
    public const string ApiKey = "sk-test-SECRET-123";

    /// <summary>Fixed instant used for seeded timestamps.</summary>
    public static readonly DateTime SeedInstant = new(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);

    public BackupTestDb(bool seed = true)
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), $"backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(DataDirectory);
        DatabasePath = Path.Combine(DataDirectory, "data.db");
        Paths = new TestPathResolver(DataDirectory, DatabasePath);
        Migrate();
        if (seed)
        {
            Seed();
        }
    }

    public string DataDirectory { get; }

    public string DatabasePath { get; }

    public IPathResolver Paths { get; }

    public string WorkDirectory => Path.Combine(DataDirectory, "backups", ".work");

    /// <summary>Lower-case hex SHA-256 of the live database file bytes.</summary>
    public string LiveSha256() => Sha256Of(DatabasePath);

    public static string Sha256Of(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    /// <summary>Opens a non-pooled connection so disposing it really releases the file.</summary>
    public static SqliteConnection Open(string path, SqliteOpenMode mode = SqliteOpenMode.ReadWrite)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false };
        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    public static void Exec(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public static string? Scalar(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Every user table of the database at <paramref name="path"/> mapped to its rows rendered as
    /// ordinal-sorted strings, so two databases can be compared table by table.
    /// </summary>
    public static Dictionary<string, List<string>> Snapshot(string path)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        using SqliteConnection connection = Open(path, SqliteOpenMode.ReadOnly);
        var tables = new List<string>();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }

        foreach (string table in tables)
        {
            var rows = new List<string>();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM \"{table}\";";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string[] cells = new string[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    cells[i] = reader.IsDBNull(i)
                        ? "<null>"
                        : reader.GetValue(i) is byte[] blob ? Convert.ToHexString(blob) : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)!;
                }

                rows.Add(string.Join('|', cells));
            }

            rows.Sort(StringComparer.Ordinal);
            result[table] = rows;
        }

        return result;
    }

    public void Dispose()
    {
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

    private AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={DatabasePath};Pooling=False")
            .Options;
        return new AppDbContext(options);
    }

    private void Migrate()
    {
        using AppDbContext context = CreateContext();
        context.Database.Migrate();
    }

    private void Seed()
    {
        using AppDbContext context = CreateContext();
        Guid categoryId = context.Categories.OrderBy(c => c.Id).Select(c => c.Id).First();
        Guid paymentMethodId = context.PaymentMethods.OrderBy(p => p.Id).Select(p => p.Id).First();

        var expense = Expense.Create(categoryId, new DateOnly(2026, 9, 3), Money.Create(1000m, Currency.DOP), "Seed expense", null, SeedInstant, paymentMethodId);
        context.Expenses.Add(expense);
        context.Incomes.Add(Income.Create(categoryId, new DateOnly(2026, 9, 10), Money.Create(5000m, Currency.DOP), "Seed income", null, null, SeedInstant, paymentMethodId));
        context.Tags.Add(Tag.Create("seed-tag", null, SeedInstant));
        context.SavedViews.Add(SavedView.Create("Seed view", TransactionType.Expense, "{}", SeedInstant));
        context.Budgets.Add(Budget.Create(2026, 9, categoryId, Money.Create(20000m, Currency.DOP)));

        var goal = Goal.Create("Seed goal", Money.Create(10000m, Currency.DOP), new DateOnly(2027, 1, 1));
        context.Goals.Add(goal);
        context.GoalContributions.Add(GoalContribution.Create(goal.Id, Money.Create(500m, Currency.DOP), new DateOnly(2026, 9, 5)));

        var debt = Debt.Create("Seed debt", Money.Create(3000m, Currency.DOP), Currency.DOP, 3, Money.Create(1000m, Currency.DOP), nameof(DebtFrequency.Monthly), new DateOnly(2026, 9, 1));
        context.Debts.Add(debt);
        context.DebtPayments.AddRange(new DebtScheduleGenerator().Generate(debt));

        context.RecurringRules.Add(Subscription.Create("Seed subscription", Money.Create(300m, Currency.DOP), Currency.DOP, nameof(RecurringFrequency.Monthly), new DateOnly(2026, 9, 1), null, categoryId, paymentMethodId));

        var alert = AnomalyAlert.Create(expense.Id, categoryId, new DateOnly(2026, 9, 3), AnomalySeverity.Info, "Seed anomaly", SeedInstant);
        context.AnomalyAlerts.Add(alert);
        context.Notifications.Add(Notification.ForAnomaly(alert.Id, new DateOnly(2026, 9, 3), "Seed anomaly", SeedInstant));

        context.Settings.Add(new Setting { Id = Guid.NewGuid(), Key = SettingKeys.AiApiKey, Value = ApiKey });
        context.Settings.Add(new Setting { Id = Guid.NewGuid(), Key = "seed.other", Value = "keep-me" });
        context.SaveChanges();
    }

    private sealed class TestPathResolver(string dataDirectory, string databasePath) : IPathResolver
    {
        public string DataDirectory => dataDirectory;

        public string DatabaseFilePath => databasePath;
    }
}
