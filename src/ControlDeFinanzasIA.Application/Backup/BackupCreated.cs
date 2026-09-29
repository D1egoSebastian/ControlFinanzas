namespace ControlDeFinanzasIA.Application.Backup;

/// <summary>Value of a successful backup creation (F-019).</summary>
/// <param name="SizeBytes">Size of the written ZIP.</param>
/// <param name="ExportedAtUtc">UTC instant recorded in the manifest.</param>
/// <param name="SchemaVersion">Latest applied <c>MigrationId</c> in the backup.</param>
public sealed record BackupCreated(long SizeBytes, DateTime ExportedAtUtc, string SchemaVersion);
