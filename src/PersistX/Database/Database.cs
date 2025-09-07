using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PersistX.Collections;
using PersistX.Interfaces;
using PersistX.Storage;

namespace PersistX.Database;

/// <summary>
/// Main database implementation for PersistX.
/// </summary>
public class Database : IDatabase
{
    private readonly ILogger<Database>? _logger;
    private readonly ConcurrentDictionary<string, object> _collections = new();
    private readonly TransactionManager _transactionManager;
    private readonly DatabaseConfiguration _configuration;
    private bool _disposed;
    private bool _initialized;

    public string Name { get; }
    public IBackend Backend { get; }
    public ITransactionManager TransactionManager => _transactionManager;

    public Database(string name, IBackend backend, DatabaseConfiguration configuration, ILogger<Database>? logger = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
        _transactionManager = new TransactionManager(logger as ILogger<TransactionManager>);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        try
        {
            var backendConfig = new DatabaseBackendConfiguration();
            if (_configuration.BackendConfiguration != null)
            {
                foreach (var kvp in _configuration.BackendConfiguration)
                {
                    backendConfig.SetValue(kvp.Key, kvp.Value);
                }
            }
            
            await Backend.InitializeAsync(backendConfig, cancellationToken);
            _initialized = true;
            _logger?.LogInformation("Database '{Name}' initialized successfully", Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize database '{Name}'", Name);
            throw;
        }
    }

    public async Task<IPersistentCollection<T>> CreateCollectionAsync<T>(
        string name,
        ISerializer<List<T>>? serializer = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name cannot be null or empty", nameof(name));

        if (_collections.ContainsKey(name))
            throw new InvalidOperationException($"Collection '{name}' already exists");

        try
        {
            // Create a persistent collection with the specified backend and serializer
            var collection = new PersistentCollection<T>(name, Backend, serializer, _logger as ILogger<PersistentCollection<T>>);
            await collection.InitializeAsync(cancellationToken);

            _collections.TryAdd(name, collection);
            _logger?.LogInformation("Created collection '{Name}' in database '{DatabaseName}'", name, Name);

            return collection;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create collection '{Name}' in database '{DatabaseName}'", name, Name);
            throw;
        }
    }

    public async Task<IPersistentCollection<T>?> GetCollectionAsync<T>(string name, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        if (string.IsNullOrWhiteSpace(name))
            return null;

        if (_collections.TryGetValue(name, out var collection) && collection is IPersistentCollection<T> typedCollection)
        {
            return await Task.FromResult(typedCollection);
        }

        // Try to load from storage
        try
        {
            var location = $"collections/{name}";
            if (await Backend.ExistsAsync(location, cancellationToken))
            {
                var loadedCollection = new PersistentCollection<T>(name, Backend, null, _logger as ILogger<PersistentCollection<T>>);
                await loadedCollection.InitializeAsync(cancellationToken);
                
                _collections.TryAdd(name, loadedCollection);
                return loadedCollection;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load collection '{Name}' from storage", name);
        }

        return null;
    }

    public async Task DropCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            // Remove from memory
            if (_collections.TryRemove(name, out var collection) && collection is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            // Remove from storage
            var location = $"collections/{name}";
            if (await Backend.ExistsAsync(location, cancellationToken))
            {
                await Backend.DeleteAsync(location, cancellationToken);
            }

            _logger?.LogInformation("Dropped collection '{Name}' from database '{DatabaseName}'", name, Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to drop collection '{Name}' from database '{DatabaseName}'", name, Name);
            throw;
        }
    }

    public async IAsyncEnumerable<string> GetCollectionNamesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        // Return collections in memory
        foreach (var name in _collections.Keys)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            yield return name;
            await Task.Yield();
        }

        // Also check for collections in storage that might not be loaded
        await foreach (var location in Backend.ListLocationsAsync("collections/*", cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            var name = location.Split('/').LastOrDefault();
            if (!string.IsNullOrEmpty(name) && !_collections.ContainsKey(name))
            {
                yield return name;
            }
            
            await Task.Yield();
        }
    }

    public async Task<ITransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        return await _transactionManager.BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<ITransaction, Task<TResult>> func,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            var result = await func(transaction);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ExecuteInTransactionAsync(
        Func<ITransaction, Task> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            await action(transaction);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        try
        {
            await Backend.FlushAsync(cancellationToken);
            _logger?.LogDebug("Database '{Name}' flushed successfully", Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to flush database '{Name}'", Name);
            throw;
        }
    }

    public async Task<DatabaseStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        var statistics = new DatabaseStatistics
        {
            CollectionCount = _collections.Count,
            ActiveTransactionCount = _transactionManager.GetActiveTransactions().Count,
            CreatedAt = DateTime.UtcNow, // This should be stored and retrieved from metadata
            LastMaintenance = DateTime.UtcNow // This should be stored and retrieved from metadata
        };

        // Calculate total storage size
        long totalSize = 0;
        await foreach (var location in Backend.ListLocationsAsync(cancellationToken: cancellationToken))
        {
            var size = await Backend.GetSizeAsync(location, cancellationToken);
            if (size > 0)
            {
                totalSize += size;
            }
        }

        statistics.TotalStorageSize = totalSize;

        return await Task.FromResult(statistics);
    }

    public async Task MaintenanceAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        try
        {
            _logger?.LogInformation("Starting maintenance for database '{Name}'", Name);

            // Rebuild indexes for all collections
            foreach (var collection in _collections.Values.OfType<IPersistentCollection<object>>())
            {
                await collection.RebuildIndexesAsync(cancellationToken);
            }

            // Flush all pending changes
            await FlushAsync(cancellationToken);

            _logger?.LogInformation("Completed maintenance for database '{Name}'", Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to perform maintenance for database '{Name}'", Name);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // Dispose all collections
            foreach (var collection in _collections.Values.OfType<IAsyncDisposable>())
            {
                await collection.DisposeAsync();
            }
            _collections.Clear();

            // Dispose transaction manager
            await _transactionManager.DisposeAsync();

            // Dispose backend
            await Backend.DisposeAsync();

            _logger?.LogInformation("Database '{Name}' disposed successfully", Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disposing database '{Name}'", Name);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Database));
        }
    }

    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Database has not been initialized. Call InitializeAsync() first.");
        }
    }
}

/// <summary>
/// Backend configuration implementation for database initialization.
/// </summary>
internal class DatabaseBackendConfiguration : IBackendConfiguration
{
    private readonly Dictionary<string, string> _configuration = new();

    public string? GetValue(string key)
    {
        return _configuration.TryGetValue(key, out var value) ? value : null;
    }

    public string GetValue(string key, string defaultValue)
    {
        return _configuration.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public IEnumerable<string> GetKeys()
    {
        return _configuration.Keys;
    }

    public void SetValue(string key, string value)
    {
        _configuration[key] = value;
    }
}
