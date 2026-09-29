namespace ControlDeFinanzasIA.Application.Backup;

/// <summary>
/// Facts about a validated backup, used to fill the restore confirmation panel (F-019, R-18).
/// </summary>
/// <param name="ExportedAtUtc">UTC instant the backup was created.</param>
/// <param name="AppVersion">Application version that produced the backup.</param>
/// <param name="SchemaVersion">Latest applied <c>MigrationId</c> of the backup.</param>
/// <param name="IsOlderSchema">The backup lacks migrations this app knows; startup will migrate it.</param>
/// <param name="LocalApiKeyWillBeLost">The backup predates the Settings table while a local key exists.</param>
/// <param name="DatabaseSizeBytes">Size of the stored database.</param>
public sealed record BackupInfo(
    DateTime ExportedAtUtc,
    string AppVersion,
    string SchemaVersion,
    bool IsOlderSchema,
    bool LocalApiKeyWillBeLost,
    long DatabaseSizeBytes);
