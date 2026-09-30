using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// F-019 R-8 ordering: safety ZIPs are named with local time, so a DST fall-back hour can make a newer file
/// sort older by name. Retention ranks by the file system write time first and uses the name only as a
/// deterministic tie-breaker.
/// </summary>
public sealed class SafetyBackupRetentionOrderTests : IDisposable
{
    private readonly RestoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Retention_KeepsTheNewestByWriteTime_EvenWhenTheirNamesSortOlder()
    {
        byte[] zip = await _h.CreateZipAsync();
        Directory.CreateDirectory(_h.BackupsDirectory);

        // Names ascend 1 -> 3 but the write times descend: the DST fall-back shape (the second 01:xx hour is
        // written later than a 01:xx name that sorts after it).
        var written = new Dictionary<string, DateTime>
        {
            ["pre-restore-20251102-013000.zip"] = new(2020, 1, 1, 3, 0, 0, DateTimeKind.Utc),
            ["pre-restore-20251102-013001.zip"] = new(2020, 1, 1, 2, 0, 0, DateTimeKind.Utc),
            ["pre-restore-20251102-013002.zip"] = new(2020, 1, 1, 1, 0, 0, DateTimeKind.Utc),
        };
        foreach ((string name, DateTime utc) in written)
        {
            string path = Path.Combine(_h.BackupsDirectory, name);
            await File.WriteAllTextAsync(path, "old " + name);
            File.SetLastWriteTimeUtc(path, utc);
        }

        Result result = await _h.RestoreAsync(zip);

        Assert.True(result.IsSuccess, result.Error?.Message);
        string[] remaining = _h.SafetyZips().Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(3, remaining.Length);
        Assert.Contains("pre-restore-20251102-013000.zip", remaining);
        Assert.Contains("pre-restore-20251102-013001.zip", remaining);
        Assert.DoesNotContain("pre-restore-20251102-013002.zip", remaining);
    }

    [Fact]
    public async Task Retention_WhenWriteTimesTie_FallsBackToTheNameOrder()
    {
        byte[] zip = await _h.CreateZipAsync();
        Directory.CreateDirectory(_h.BackupsDirectory);
        DateTime same = new(2020, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        string[] names = ["pre-restore-20200101-000001.zip", "pre-restore-20200101-000002.zip", "pre-restore-20200101-000003.zip"];
        foreach (string name in names)
        {
            string path = Path.Combine(_h.BackupsDirectory, name);
            await File.WriteAllTextAsync(path, "old " + name);
            File.SetLastWriteTimeUtc(path, same);
        }

        Result result = await _h.RestoreAsync(zip);

        Assert.True(result.IsSuccess, result.Error?.Message);
        string[] remaining = _h.SafetyZips().Select(Path.GetFileName).ToArray()!;
        Assert.DoesNotContain(names[0], remaining);
        Assert.Contains(names[1], remaining);
        Assert.Contains(names[2], remaining);
    }

    [Fact]
    public async Task Retention_NeverTouchesFilesThatAreNotSafetyZipNames_EvenWhenTheyAreOlder()
    {
        byte[] zip = await _h.CreateZipAsync();
        Directory.CreateDirectory(_h.BackupsDirectory);
        string[] foreign = ["pre-restore-notes.zip", "my-backup.zip", "pre-restore-20200101-000001.zipx"];
        foreach (string name in foreign)
        {
            string path = Path.Combine(_h.BackupsDirectory, name);
            await File.WriteAllTextAsync(path, "foreign " + name);
            File.SetLastWriteTimeUtc(path, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        Result result = await _h.RestoreAsync(zip);

        Assert.True(result.IsSuccess, result.Error?.Message);
        foreach (string name in foreign)
        {
            Assert.Equal("foreign " + name, await File.ReadAllTextAsync(Path.Combine(_h.BackupsDirectory, name)));
        }
    }
}
