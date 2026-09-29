namespace ControlDeFinanzasIA.Application.Backup;

/// <summary>Value of a successful restore (F-019).</summary>
/// <param name="SafetyBackupFileName">File name of the pre-restore safety ZIP.</param>
/// <param name="ApiKeyCarriedOver">The local API key was written into the restored database.</param>
/// <param name="ApiKeyLost">The backup predates Settings, so a local key could not be kept.</param>
/// <param name="IsOlderSchema">The restored database will be migrated at the next start.</param>
public sealed record RestoreOutcome(
    string SafetyBackupFileName,
    bool ApiKeyCarriedOver,
    bool ApiKeyLost,
    bool IsOlderSchema);
