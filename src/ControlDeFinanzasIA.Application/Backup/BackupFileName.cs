using System.Globalization;

namespace ControlDeFinanzasIA.Application.Backup;

/// <summary>Builds the suggested file name of a user backup (F-019, R-2, D-18).</summary>
public static class BackupFileName
{
    /// <summary>
    /// Returns <c>ControlDeFinanzasIA-backup-yyyyMMdd-HHmm.zip</c> in the given zone's local time,
    /// always formatted with the invariant culture.
    /// </summary>
    /// <param name="utcNow">A UTC instant.</param>
    /// <param name="zone">The zone whose local time is used in the name.</param>
    public static string Create(DateTime utcNow, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"ControlDeFinanzasIA-backup-{local:yyyyMMdd-HHmm}.zip");
    }
}
