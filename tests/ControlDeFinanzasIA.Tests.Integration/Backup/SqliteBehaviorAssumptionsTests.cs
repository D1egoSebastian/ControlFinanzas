using System.Text;
using Microsoft.Data.Sqlite;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// Characterization tests (F-019, task 2.1) that pin the Microsoft.Data.Sqlite / SQLite behavior the
/// backup design relies on, against the pinned package version: online <c>BackupDatabase</c> under a
/// concurrent writer, <c>Pooling=False</c> handle release, and <c>secure_delete</c> + <c>VACUUM</c>
/// redaction. Only temp directories are used.
/// </summary>
public sealed class SqliteBehaviorAssumptionsTests : IDisposable
{
    private const string _marker = "MARKER-sk-test-SECRET-9f8e7d6c5b4a-MARKER";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sqlite-assume-{Guid.NewGuid():N}");

    public SqliteBehaviorAssumptionsTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    [Fact]
    public async Task BackupDatabase_WithConcurrentWriter_ProducesAConsistentCopy()
    {
        string live = Path.Combine(_dir, "live.db");
        using (SqliteConnection setup = Open(live, "ReadWriteCreate"))
        {
            Exec(setup, "CREATE TABLE T (Id INTEGER PRIMARY KEY, Payload TEXT NOT NULL);");
            Exec(setup, "CREATE INDEX IX_T_Payload ON T (Payload);");
            for (int i = 0; i < 500; i++)
            {
                Exec(setup, $"INSERT INTO T (Payload) VALUES ('seed-{i}');");
            }
        }

        using var stop = new CancellationTokenSource();
        var writer = Task.Run(() =>
        {
            using SqliteConnection w = Open(live, "ReadWrite");
            int n = 0;
            while (!stop.IsCancellationRequested)
            {
                Exec(w, $"INSERT INTO T (Payload) VALUES ('live-{n++}');");
                Thread.Sleep(5);
            }
        });

        try
        {
            for (int round = 0; round < 5; round++)
            {
                string copy = Path.Combine(_dir, $"copy-{round}.db");
                CopyRetryingWhileBusy(live, copy);

                using SqliteConnection check = Open(copy, "ReadOnly");
                Assert.Equal("ok", Scalar(check, "PRAGMA integrity_check;"));
                Assert.True(Convert.ToInt64(Scalar(check, "SELECT count(*) FROM T;"), System.Globalization.CultureInfo.InvariantCulture) >= 500);
                await Task.Delay(10);
            }
        }
        finally
        {
            await stop.CancelAsync();
            await writer;
        }
    }

    [Fact]
    public void PoolingFalse_ReleasesTheFileHandleOnDispose()
    {
        string path = Path.Combine(_dir, "nopool.db");
        using (SqliteConnection c = Open(path, "ReadWriteCreate"))
        {
            Exec(c, "CREATE TABLE T (Id INTEGER);");
        }

        File.Delete(path);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void PoolingOn_KeepsTheHandleUntilClearAllPools_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string path = Path.Combine(_dir, "pooled.db");
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = true };
        // ClearAllPools is process-wide and other fixtures call it while running in parallel; if one clears
        // the pool between the close and the delete, the file is deletable for the wrong reason. Arm again.
        bool held = false;
        for (int attempt = 0; attempt < 50 && !held; attempt++)
        {
            using (var c = new SqliteConnection(builder.ConnectionString))
            {
                c.Open();
                Exec(c, "CREATE TABLE IF NOT EXISTS T (Id INTEGER);");
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                held = true;
            }
        }

        Assert.True(held, "A pooled connection must keep the OS handle open after it was closed.");

        SqliteConnection.ClearAllPools();
        File.Delete(path);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void SecureDeleteAndVacuum_LeaveNoTraceOfTheDeletedValue_WhilePlainDeleteDoes()
    {
        string plain = Path.Combine(_dir, "plain.db");
        string redacted = Path.Combine(_dir, "redacted.db");

        foreach (string path in new[] { plain, redacted })
        {
            using SqliteConnection c = Open(path, "ReadWriteCreate");
            Exec(c, "CREATE TABLE Settings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);");
            Exec(c, "INSERT INTO Settings VALUES ('other', 'keep-me');");
            Exec(c, $"INSERT INTO Settings VALUES ('ai.apiKey', '{_marker}');");
        }

        using (SqliteConnection c = Open(plain, "ReadWrite"))
        {
            Exec(c, "DELETE FROM Settings WHERE Key = 'ai.apiKey';");
        }

        using (SqliteConnection c = Open(redacted, "ReadWrite"))
        {
            Exec(c, "PRAGMA secure_delete = ON;");
            Exec(c, "DELETE FROM Settings WHERE Key = 'ai.apiKey';");
            Exec(c, "VACUUM;");
        }

        Assert.True(ContainsBytes(File.ReadAllBytes(plain), Encoding.UTF8.GetBytes(_marker)), "A plain DELETE is expected to leave the value in the file.");
        Assert.False(ContainsBytes(File.ReadAllBytes(redacted), Encoding.UTF8.GetBytes(_marker)));
        using SqliteConnection verify = Open(redacted, "ReadOnly");
        Assert.Equal("keep-me", Scalar(verify, "SELECT Value FROM Settings WHERE Key = 'other';"));
    }

    /// <summary>
    /// FINDING (pinned Microsoft.Data.Sqlite 8.0.11): <c>BackupDatabase</c> does not wait on a busy
    /// source. When a writer holds a PENDING/EXCLUSIVE lock at that instant it throws SQLite error 5
    /// immediately, even with the default 30 s busy timeout. The copy is consistent whenever it
    /// succeeds, so the service retries on BUSY/LOCKED (5/6) with a fresh destination file.
    /// </summary>
    private static void CopyRetryingWhileBusy(string live, string copy)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using SqliteConnection src = Open(live, "ReadOnly");
                using SqliteConnection dst = Open(copy, "ReadWriteCreate");
                src.BackupDatabase(dst);
                return;
            }
            catch (SqliteException ex) when ((ex.SqliteErrorCode & 0xFF) is 5 or 6 && attempt < 50)
            {
                File.Delete(copy);
                Thread.Sleep(20);
            }
        }
    }

    private static bool ContainsBytes(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;

    private static SqliteConnection Open(string path, string mode)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = Enum.Parse<SqliteOpenMode>(mode),
            Pooling = false,
        };
        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    private static void Exec(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string? Scalar(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
