namespace ControlDeFinanzasIA.Application.Backup;

/// <summary>
/// The <c>manifest.json</c> stored in every backup ZIP (F-019, R-2).
/// </summary>
/// <param name="FormatVersion">Backup format version; currently <c>1</c>.</param>
/// <param name="SchemaVersion">Latest applied <c>MigrationId</c> of the stored database.</param>
/// <param name="ExportedAt">UTC instant of the export.</param>
/// <param name="AppVersion">Version of the application that produced the backup.</param>
/// <param name="DbSha256">Lower-case hex SHA-256 of the stored <c>data.db</c> bytes.</param>
/// <param name="ApiKeyIncluded">Whether the stored database still holds the AI API key.</param>
public sealed record BackupManifest(
    int FormatVersion,
    string SchemaVersion,
    DateTime ExportedAt,
    string AppVersion,
    string DbSha256,
    bool ApiKeyIncluded);
