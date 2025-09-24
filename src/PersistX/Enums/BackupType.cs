namespace PersistX.Enums;

/// <summary>
/// Types of backups.
/// </summary>
public enum BackupType
{
    /// <summary>
    /// Full backup containing all data.
    /// </summary>
    Full,

    /// <summary>
    /// Incremental backup containing only changes since the base backup.
    /// </summary>
    Incremental
}
