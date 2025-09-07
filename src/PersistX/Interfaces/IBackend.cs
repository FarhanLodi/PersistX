using System.Buffers;

namespace PersistX.Interfaces;

/// <summary>
/// Represents a storage backend for PersistX collections.
/// </summary>
public interface IBackend : IAsyncDisposable
{
    /// <summary>
    /// Gets the name of this backend.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Initializes the backend with the specified configuration.
    /// </summary>
    /// <param name="configuration">Backend-specific configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(IBackendConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads data from the specified location.
    /// </summary>
    /// <param name="location">Storage location identifier</param>
    /// <param name="offset">Offset within the location</param>
    /// <param name="length">Number of bytes to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Read data as a memory buffer</returns>
    Task<ReadOnlyMemory<byte>> ReadAsync(string location, long offset, int length, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes data to the specified location.
    /// </summary>
    /// <param name="location">Storage location identifier</param>
    /// <param name="offset">Offset within the location</param>
    /// <param name="data">Data to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WriteAsync(string location, long offset, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes data at the specified location.
    /// </summary>
    /// <param name="location">Storage location identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(string location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a location exists.
    /// </summary>
    /// <param name="location">Storage location identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the location exists</returns>
    Task<bool> ExistsAsync(string location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the size of data at the specified location.
    /// </summary>
    /// <param name="location">Storage location identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Size in bytes, or -1 if location doesn't exist</returns>
    Task<long> GetSizeAsync(string location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all locations in the backend.
    /// </summary>
    /// <param name="pattern">Optional pattern to filter locations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of location identifiers</returns>
    IAsyncEnumerable<string> ListLocationsAsync(string? pattern = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any pending writes to persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration interface for storage backends.
/// </summary>
public interface IBackendConfiguration
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
