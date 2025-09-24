using System.Buffers;

namespace PersistX.Interfaces;

/// <summary>
/// Represents a Write-Ahead Log (WAL) for crash recovery and durability.
/// </summary>
public interface IWriteAheadLog : IAsyncDisposable
{
    /// <summary>
    /// Gets the name of this WAL implementation.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Initializes the WAL with the specified configuration.
    /// </summary>
    /// <param name="configuration">WAL-specific configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(IWriteAheadLogConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a write operation before it's applied to the main storage.
    /// </summary>
    /// <param name="location">Storage location identifier</param>
    /// <param name="offset">Offset within the location</param>
    /// <param name="data">Data to be written</param>
    /// <param name="transactionId">Transaction identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Log entry identifier</returns>
    Task<long> LogWriteAsync(string location, long offset, ReadOnlyMemory<byte> data, long transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a delete operation before it's applied to the main storage.
    /// </summary>
    /// <param name="location">Storage location identifier</param>
    /// <param name="transactionId">Transaction identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Log entry identifier</returns>
    Task<long> LogDeleteAsync(string location, long transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a transaction commit.
    /// </summary>
    /// <param name="transactionId">Transaction identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LogCommitAsync(long transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a transaction rollback.
    /// </summary>
    /// <param name="transactionId">Transaction identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LogRollbackAsync(long transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replays all committed operations from the WAL to recover from a crash.
    /// </summary>
    /// <param name="backend">Backend to replay operations to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReplayAsync(IBackend backend, CancellationToken cancellationToken = default);

    /// <summary>
    /// Truncates the WAL up to the specified log entry.
    /// </summary>
    /// <param name="upToLogEntry">Log entry to truncate up to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task TruncateAsync(long upToLogEntry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current WAL size in bytes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>WAL size in bytes</returns>
    Task<long> GetSizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any pending writes to persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration interface for Write-Ahead Log implementations.
/// </summary>
public interface IWriteAheadLogConfiguration
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
