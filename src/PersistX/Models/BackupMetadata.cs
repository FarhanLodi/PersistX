using PersistX.Enums;

namespace PersistX.Models;

/// <summary>
/// Metadata about a backup.
/// </summary>
public record BackupMetadata
{
    /// <summary>
    /// Unique backup identifier.
    /// </summary>
    public string BackupId { get; init; } = string.Empty;

    /// <summary>
    /// Type of backup (Full, Incremental).
    /// </summary>
    public BackupType Type { get; init; }

    /// <summary>
    /// Base backup identifier for incremental backups.
    /// </summary>
    public string? BaseBackupId { get; init; }

    /// <summary>
    /// Timestamp when the backup was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Size of the backup in bytes.
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// Number of locations backed up.
    /// </summary>
    public int LocationCount { get; init; }

    /// <summary>
    /// Backup status.
    /// </summary>
    public BackupStatus Status { get; init; }

    /// <summary>
    /// Optional description of the backup.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Checksum for integrity validation.
    /// </summary>
    public string? Checksum { get; init; }
}
