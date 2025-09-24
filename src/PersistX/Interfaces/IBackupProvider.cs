using System.Buffers;
using PersistX.Enums;
using PersistX.Models;

namespace PersistX.Interfaces;

/// <summary>
/// Represents a backup provider for automated backup and point-in-time recovery.
/// </summary>
public interface IBackupProvider : IAsyncDisposable
{
    /// <summary>
    /// Gets the name of this backup provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Initializes the backup provider with the specified configuration.
    /// </summary>
    /// <param name="configuration">Backup configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(IBackupProviderConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a full backup of the specified backend.
    /// </summary>
    /// <param name="backend">Backend to backup</param>
    /// <param name="backupId">Unique backup identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backup metadata</returns>
    Task<BackupMetadata> CreateBackupAsync(IBackend backend, string backupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an incremental backup of the specified backend.
    /// </summary>
    /// <param name="backend">Backend to backup</param>
    /// <param name="backupId">Unique backup identifier</param>
    /// <param name="baseBackupId">Base backup to increment from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backup metadata</returns>
    Task<BackupMetadata> CreateIncrementalBackupAsync(IBackend backend, string backupId, string baseBackupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores data from a backup to the specified backend.
    /// </summary>
    /// <param name="backend">Backend to restore to</param>
    /// <param name="backupId">Backup identifier to restore from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RestoreBackupAsync(IBackend backend, string backupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all available backups.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of backup metadata</returns>
    IAsyncEnumerable<BackupMetadata> ListBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata for a specific backup.
    /// </summary>
    /// <param name="backupId">Backup identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backup metadata, or null if not found</returns>
    Task<BackupMetadata?> GetBackupMetadataAsync(string backupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a backup.
    /// </summary>
    /// <param name="backupId">Backup identifier to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteBackupAsync(string backupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the integrity of a backup.
    /// </summary>
    /// <param name="backupId">Backup identifier to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the backup is valid</returns>
    Task<bool> ValidateBackupAsync(string backupId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration interface for backup providers.
/// </summary>
public interface IBackupProviderConfiguration
{
    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration value, or null if not found</returns>
    string? GetValue(string key);

    /// <summary>
    /// Gets a configuration value by key with a default value.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if key not found</param>
    /// <returns>Configuration value or default</returns>
    string GetValue(string key, string defaultValue);

    /// <summary>
    /// Gets all configuration keys.
    /// </summary>
    /// <returns>Collection of configuration keys</returns>
    IEnumerable<string> GetKeys();
}

