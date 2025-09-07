using System.Buffers;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;
using PersistX.Indexes;
using PersistX.Serialization;

namespace PersistX.Collections;

/// <summary>
/// Enterprise-grade persistent collection that stores data using a backend with advanced features.
/// </summary>
/// <typeparam name="T">The type of elements in the collection</typeparam>
public class PersistentCollection<T> : IPersistentCollection<T>
{
    private readonly string _name;
    private readonly IBackend _backend;
    private readonly ISerializer<List<T>> _serializer;
    private readonly ILogger<PersistentCollection<T>>? _logger;
    private readonly ConcurrentDictionary<string, object> _indexes = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;
    private bool _initialized;
    private long _count = 0;

    public string Name => _name;
    public bool IsReadOnly => false;
    public ISerializer<T> Serializer => throw new NotSupportedException("Use the collection's internal serializer for List<T>");

    public Task<long> CountAsync => Task.FromResult(_count);

    public PersistentCollection(
        string name, 
        IBackend backend, 
        ISerializer<List<T>>? serializer = null,
        ILogger<PersistentCollection<T>>? logger = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _serializer = serializer ?? new JsonSerializer<List<T>>();
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        try
        {
            // Initialize the collection storage location
            var location = $"{_name}.data";
            if (!await _backend.ExistsAsync(location, cancellationToken))
            {
                // Create initial metadata
                await SaveMetadataAsync(cancellationToken);
            }
            else
            {
                // Load existing metadata
                await LoadMetadataAsync(cancellationToken);
            }

            _initialized = true;
            _logger?.LogDebug("Initialized collection '{Name}'", _name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize collection '{Name}'", _name);
            throw;
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Clear all data
            var location = $"{_name}.data";
            if (await _backend.ExistsAsync(location, cancellationToken))
            {
                await _backend.DeleteAsync(location, cancellationToken);
            }

            // Clear all indexes
            foreach (var index in _indexes.Values)
            {
                if (index is IIndex<object, T> typedIndex)
                {
                    await typedIndex.ClearAsync(cancellationToken);
                }
            }

            _count = 0;
            await SaveMetadataAsync(cancellationToken);

            _logger?.LogDebug("Cleared collection '{Name}'", _name);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ContainsAsync(T item, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        // For a basic implementation, we'll iterate through all items
        // In a real implementation, this would use indexes for efficiency
        await foreach (var existingItem in GetAllAsync(cancellationToken))
        {
            if (EqualityComparer<T>.Default.Equals(existingItem, item))
                return true;
        }

        return false;
    }

    public async Task AddAsync(T item, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var location = $"{_name}.data";
            var items = new List<T>();
            
            if (await _backend.ExistsAsync(location, cancellationToken))
            {
                var size = await _backend.GetSizeAsync(location, cancellationToken);
                if (size > 0)
                {
                    var data = await _backend.ReadAsync(location, 0, (int)size, cancellationToken);
                    items = await _serializer.DeserializeAsync(data, cancellationToken);
                }
            }

            items.Add(item);
            _count = items.Count;

            // Serialize the list
            var buffer = new ArrayBufferWriter<byte>();
            await _serializer.SerializeAsync(items, buffer, cancellationToken);
            await _backend.WriteAsync(location, 0, buffer.WrittenMemory, cancellationToken);
            await SaveMetadataAsync(cancellationToken);

            // Update indexes
            foreach (var index in _indexes.Values)
            {
                if (index is IIndex<object, T> typedIndex)
                {
                    // Extract the key using the key selector
                    var keySelector = typedIndex.KeySelector;
                    var key = keySelector.Compile()(item);
                    await typedIndex.AddAsync(key, item, cancellationToken);
                }
            }

            _logger?.LogDebug("Added item to collection '{Name}'", _name);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddRangeAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var location = $"{_name}.data";
            var existingItems = new List<T>();
            
            if (await _backend.ExistsAsync(location, cancellationToken))
            {
                var size = await _backend.GetSizeAsync(location, cancellationToken);
                if (size > 0)
                {
                    var data = await _backend.ReadAsync(location, 0, (int)size, cancellationToken);
                    existingItems = await _serializer.DeserializeAsync(data, cancellationToken);
                }
            }

            // Add all new items
            existingItems.AddRange(items);
            _count = existingItems.Count;

            // Serialize the list
            var buffer = new ArrayBufferWriter<byte>();
            await _serializer.SerializeAsync(existingItems, buffer, cancellationToken);
            await _backend.WriteAsync(location, 0, buffer.WrittenMemory, cancellationToken);
            await SaveMetadataAsync(cancellationToken);

            // Update indexes for all items
            foreach (var index in _indexes.Values)
            {
                if (index is IIndex<object, T> typedIndex)
                {
                    foreach (var item in items)
                    {
                        var keySelector = typedIndex.KeySelector;
                        var key = keySelector.Compile()(item);
                        await typedIndex.AddAsync(key, item, cancellationToken);
                    }
                }
            }

            _logger?.LogDebug("Added {Count} items to collection '{Name}'", items.Count(), _name);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async IAsyncEnumerable<T> GetAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        var location = $"{_name}.data";
        if (!await _backend.ExistsAsync(location, cancellationToken))
            yield break;

        List<T> items;
        try
        {
            var size = await _backend.GetSizeAsync(location, cancellationToken);
            if (size <= 0)
                yield break;

            var data = await _backend.ReadAsync(location, 0, (int)size, cancellationToken);
            items = await DeserializeItemsAsync(data, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read items from collection '{Name}'", _name);
            throw;
        }

        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
            yield return item;
            await Task.Yield();
        }
    }

    public async Task<IIndex<TKey, T>> CreateIndexAsync<TKey>(
        string name,
        Expression<Func<T, TKey>> keySelector,
        IndexConfiguration? configuration = null,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Index name cannot be null or empty", nameof(name));

        if (_indexes.ContainsKey(name))
            throw new InvalidOperationException($"Index '{name}' already exists");

        try
        {
            // For now, create a hash index
            // In a full implementation, this would create the appropriate index type based on configuration
            var index = new HashIndex<TKey, T>(name, keySelector, configuration, _logger as ILogger<HashIndex<TKey, T>>);
            await index.InitializeAsync(cancellationToken);

            // Store the index in the dictionary
            _indexes.TryAdd(name, index);

            _logger?.LogDebug("Created index '{IndexName}' on collection '{CollectionName}'", name, _name);

            return index;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create index '{IndexName}' on collection '{CollectionName}'", name, _name);
            throw;
        }
    }

    public async Task<IIndex<TKey, T>?> GetIndexAsync<TKey>(string name, CancellationToken cancellationToken = default) where TKey : notnull
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        if (_indexes.TryGetValue(name, out var index) && index is IIndex<TKey, T> typedIndex)
        {
            return await Task.FromResult(typedIndex);
        }

        return null;
    }

    public async Task DropIndexAsync(string name, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        if (_indexes.TryRemove(name, out var index) && index is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            _logger?.LogDebug("Dropped index '{IndexName}' from collection '{CollectionName}'", name, _name);
        }
    }

    public async IAsyncEnumerable<string> GetIndexNamesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        foreach (var name in _indexes.Keys)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
            yield return name;
            await Task.Yield();
        }
    }

    public async Task RebuildIndexesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        try
        {
            _logger?.LogDebug("Rebuilding indexes for collection '{Name}'", _name);

            foreach (var index in _indexes.Values)
            {
                if (index is IIndex<object, T> typedIndex)
                {
                    await typedIndex.RebuildAsync(GetAllAsync(cancellationToken), cancellationToken);
                }
            }

            _logger?.LogDebug("Completed rebuilding indexes for collection '{Name}'", _name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to rebuild indexes for collection '{Name}'", _name);
            throw;
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        try
        {
            await _backend.FlushAsync(cancellationToken);
            _logger?.LogDebug("Flushed collection '{Name}'", _name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to flush collection '{Name}'", _name);
            throw;
        }
    }

    public async Task<CollectionStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        var statistics = new CollectionStatistics
        {
            ElementCount = _count,
            IndexCount = _indexes.Count,
            CreatedAt = DateTime.UtcNow, // This should be stored and retrieved from metadata
            LastModified = DateTime.UtcNow // This should be stored and retrieved from metadata
        };

        // Calculate storage size
        long storageSize = 0;
        var dataLocation = $"{_name}.data";
        var metadataLocation = $"{_name}.metadata";
        
        if (await _backend.ExistsAsync(dataLocation, cancellationToken))
        {
            storageSize += await _backend.GetSizeAsync(dataLocation, cancellationToken);
        }
        
        if (await _backend.ExistsAsync(metadataLocation, cancellationToken))
        {
            storageSize += await _backend.GetSizeAsync(metadataLocation, cancellationToken);
        }

        statistics.StorageSize = storageSize;

        return await Task.FromResult(statistics);
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var item in GetAllAsync(cancellationToken))
        {
            yield return item;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // Dispose all indexes
            foreach (var index in _indexes.Values)
            {
                if (index is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
            }
            _indexes.Clear();

            _lock.Dispose();

            _logger?.LogDebug("Disposed collection '{Name}'", _name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disposing collection '{Name}'", _name);
        }
    }

    private async Task SaveMetadataAsync(CancellationToken cancellationToken)
    {
        var metadata = new CollectionMetadata
        {
            Name = _name,
            Count = _count,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        var location = $"{_name}.metadata";
        var serializer = new JsonSerializer<CollectionMetadata>();
        
        var buffer = new ArrayBufferWriter<byte>();
        await serializer.SerializeAsync(metadata, buffer, cancellationToken);
        
        await _backend.WriteAsync(location, 0, buffer.WrittenMemory, cancellationToken);
    }

    private async Task LoadMetadataAsync(CancellationToken cancellationToken)
    {
        var location = $"{_name}.metadata";
        if (!await _backend.ExistsAsync(location, cancellationToken))
            return;

        try
        {
            var size = await _backend.GetSizeAsync(location, cancellationToken);
            var data = await _backend.ReadAsync(location, 0, (int)size, cancellationToken);
            
            var serializer = new JsonSerializer<CollectionMetadata>();
            var metadata = await serializer.DeserializeAsync(data, cancellationToken);
            
            _count = metadata.Count;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load metadata for collection '{Name}', using defaults", _name);
        }
    }

    private async Task<List<T>> DeserializeItemsAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        try
        {
            return await _serializer.DeserializeAsync(data, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to deserialize items for collection '{Name}'", _name);
            return new List<T>();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PersistentCollection<T>));
        }
    }

    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Collection has not been initialized. Call InitializeAsync() first.");
        }
    }
}

/// <summary>
/// Metadata for a persistent collection.
/// </summary>
internal class CollectionMetadata
{
    public string Name { get; set; } = string.Empty;
    public long Count { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
}
