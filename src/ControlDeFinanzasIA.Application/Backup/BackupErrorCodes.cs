namespace ControlDeFinanzasIA.Application.Backup;

/// <summary>Stable failure codes returned by the backup use cases (F-019, R-17).</summary>
public static class BackupErrorCodes
{
    /// <summary>The input is not a valid backup archive.</summary>
    public const string NotABackup = "NotABackup";

    /// <summary>The archive or database is damaged (hash, integrity or foreign-key failure).</summary>
    public const string Corrupt = "Corrupt";

    /// <summary>The backup comes from a newer application version.</summary>
    public const string NewerSchema = "NewerSchema";

    /// <summary>Reading, writing or swapping files failed, including insufficient space.</summary>
    public const string IoError = "IoError";

    /// <summary>Detail key: the backup's latest migration id.</summary>
    public const string DetailBackupSchema = "backupSchema";

    /// <summary>Detail key: this application's latest migration id.</summary>
    public const string DetailAppSchema = "appSchema";

    /// <summary>Detail key: the application version that produced the backup.</summary>
    public const string DetailBackupAppVersion = "backupAppVersion";

    /// <summary>Detail key: a sub-reason refining the code.</summary>
    public const string DetailReason = "reason";

    /// <summary>Detail value for <see cref="DetailReason"/>: the volume lacks free space.</summary>
    public const string ReasonInsufficientSpace = "insufficientSpace";
}
