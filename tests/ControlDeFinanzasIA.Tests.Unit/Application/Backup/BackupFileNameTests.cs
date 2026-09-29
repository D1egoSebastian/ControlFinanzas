using System.Globalization;
using ControlDeFinanzasIA.Application.Backup;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Backup;

/// <summary>Tests for <see cref="BackupFileName"/> (F-019, R-2, S-4, D-18).</summary>
public sealed class BackupFileNameTests
{
    private static readonly TimeZoneInfo _utcMinusFour =
        TimeZoneInfo.CreateCustomTimeZone("test-utc-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4");

    [Fact]
    public void Create_UsesLocalTimeAndExactPattern()
    {
        var utc = new DateTime(2026, 9, 29, 15, 7, 45, DateTimeKind.Utc);

        string name = BackupFileName.Create(utc, _utcMinusFour);

        Assert.Equal("ControlDeFinanzasIA-backup-20260929-1107.zip", name);
    }

    [Fact]
    public void Create_WhenZoneCrossesMidnight_ChangesTheDay()
    {
        var utc = new DateTime(2026, 10, 1, 2, 30, 0, DateTimeKind.Utc);

        string name = BackupFileName.Create(utc, _utcMinusFour);

        Assert.Equal("ControlDeFinanzasIA-backup-20260930-2230.zip", name);
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    [InlineData("ar-SA")]
    public void Create_UsesInvariantDigitsRegardlessOfCurrentCulture(string culture)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            var utc = new DateTime(2026, 1, 2, 8, 5, 0, DateTimeKind.Utc);

            string name = BackupFileName.Create(utc, TimeZoneInfo.Utc);

            Assert.Equal("ControlDeFinanzasIA-backup-20260102-0805.zip", name);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
