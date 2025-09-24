namespace PersistX.Enums;

/// <summary>
/// Status of a backup.
/// </summary>
public enum BackupStatus
{
    /// <summary>
    /// Backup is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Backup completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Backup failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Backup is being restored.
    /// </summary>
    Restoring
}
