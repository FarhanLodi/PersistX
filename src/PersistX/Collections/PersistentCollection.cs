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
    // -----------------------------------------------------------------------
    // BUG 1 FIX: Non-generic wrapper so typed indexes can be updated without
    // a covariant cast that always fails for closed generic IIndex<TKey,T>.
    // The updater also covers ClearAsync and RebuildAsync so no external class
    // needs to implement bridge interfaces.
    // -----------------------------------------------------------------------
    private interface IIndexUpdater
    {
        Task OnItemAddedAsync(T item, CancellationToken ct);
        Task OnItemRemovedAsync(T item, CancellationToken ct);
        Task ClearAsync(CancellationToken ct);
        Task RebuildAsync(IAsyncEnumerable<T> data, CancellationToken ct);
    }

    private sealed class IndexUpdater<TKey> : IIndexUpdater where TKey : notnull
    {
        private readonly IIndex<TKey, T> _index;
        private readonly Func<T, TKey> _keySelector;

        internal IndexUpdater(IIndex<TKey, T> index)
        {
            _index = index;
            _keySelector = index.KeySelector.Compile();
        }

        public Task OnItemAddedAsync(T item, CancellationToken ct)
            => _index.AddAsync(_keySelector(item), item, ct);

        public Task OnItemRemovedAsync(T item, CancellationToken ct)
            => _index.RemoveAsync(_keySelector(item), item, ct);

        public Task ClearAsync(CancellationToken ct)
            => _index.ClearAsync(ct);

        public Task RebuildAsync(IAsyncEnumerable<T> data, CancellationToken ct)
            => _index.RebuildAsync(data, ct);
    }

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------
    private readonly string _name;
    private readonly IBackend _backend;
    private readonly ISerializer<List<T>> _serializer;
    private readonly ILogger<PersistentCollection<T>>? _logger;

    // BUG 1 FIX: store both the raw index object and its type-erased updater.
    private readonly ConcurrentDictionary<string, (object index, IIndexUpdater updater)> _indexes = new();

    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;
    private bool _initialized;
    private long _count = 0;

    // BUG 3 FIX: track real creation / modification times instead of always
    // returning DateTime.UtcNow.
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _lastModified = DateTime.UtcNow;

    // -----------------------------------------------------------------------
    // IPersistentCollection<T> properties
    // -----------------------------------------------------------------------
    public string Name => _name;
    public bool IsReadOnly => false;

    // BUG 5 FIX: The interface no longer has a Serializer property — removed.

    public Task<long> CountAsync => Task.FromResult(_count);

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------
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

    // -----------------------------------------------------------------------
    // Initialization
    // -----------------------------------------------------------------------
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        try
        {
            var location = $"{_name}.data";
            if (!await _backend.ExistsAsync(location, cancellationToken))
            {
                await SaveMetadataAsync(cancellationToken);
            }
            else
            {
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

    // -----------------------------------------------------------------------
    // ClearAsync
    // -----------------------------------------------------------------------
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        // BUG 4 FIX
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var location = $"{_name}.data";
            if (await _backend.ExistsAsync(location, cancellationToken))
                await _backend.DeleteAsync(location, cancellationToken);

            // Clear all indexes via the type-erased updater.
            foreach (var (_, updater) in _indexes.Values)
                await updater.ClearAsync(cancellationToken);

            _count = 0;
            _lastModified = DateTime.UtcNow;
            await SaveMetadataAsync(cancellationToken);

            _logger?.LogDebug("Cleared collection '{Name}'", _name);
        }
        finally
        {
            _lock.Release();
        }
    }

    // -----------------------------------------------------------------------
    // ContainsAsync
    // -----------------------------------------------------------------------
    public async Task<bool> ContainsAsync(T item, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        await foreach (var existingItem in GetAllAsync(cancellationToken))
        {
            if (EqualityComparer<T>.Default.Equals(existingItem, item))
                return true;
        }

        return false;
    }

    // -----------------------------------------------------------------------
    // AddAsync
    // -----------------------------------------------------------------------
    public async Task AddAsync(T item, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var items = await LoadItemsLockedAsync(cancellationToken);
            items.Add(item);
            _count = items.Count;

            await PersistItemsLockedAsync(items, cancellationToken);

            // BUG 1 FIX: use the typed IIndexUpdater instead of unsafe cast.
            foreach (var (_, updater) in _indexes.Values)
                await updater.OnItemAddedAsync(item, cancellationToken);

            _logger?.LogDebug("Added item to collection '{Name}'", _name);
        }
        finally
        {
            _lock.Release();
        }
    }

    // -----------------------------------------------------------------------
    // AddRangeAsync
    // -----------------------------------------------------------------------
    public async Task AddRangeAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var existingItems = await LoadItemsLockedAsync(cancellationToken);
            var newItems = items.ToList();
            existingItems.AddRange(newItems);
            _count = existingItems.Count;

            await PersistItemsLockedAsync(existingItems, cancellationToken);

            // BUG 1 FIX: use the typed IIndexUpdater.
            foreach (var newItem in newItems)
            {
                foreach (var (_, updater) in _indexes.Values)
                    await updater.OnItemAddedAsync(newItem, cancellationToken);
            }

            _logger?.LogDebug("Added {Count} items to collection '{Name}'", newItems.Count, _name);
        }
        finally
        {
            _lock.Release();
        }
    }

    // -----------------------------------------------------------------------
    // RemoveAsync  (NEW METHOD)
    // -----------------------------------------------------------------------
    public async Task<bool> RemoveAsync(T item, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var items = await LoadItemsLockedAsync(cancellationToken);
            int index = items.FindIndex(x => EqualityComparer<T>.Default.Equals(x, item));
            if (index < 0)
                return false;

            var removed = items[index];
            items.RemoveAt(index);
            _count = items.Count;

            await PersistItemsLockedAsync(items, cancellationToken);

            foreach (var (_, updater) in _indexes.Values)
                await updater.OnItemRemovedAsync(removed, cancellationToken);

            _logger?.LogDebug("Removed item from collection '{Name}'", _name);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    // -----------------------------------------------------------------------
    // RemoveWhereAsync  (NEW METHOD)
    // -----------------------------------------------------------------------
    public async Task<int> RemoveWhereAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        if (predicate is null) throw new ArgumentNullException(nameof(predicate));

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var items = await LoadItemsLockedAsync(cancellationToken);
            var removed = items.Where(predicate).ToList();
            if (removed.Count == 0)
                return 0;

            items.RemoveAll(x => predicate(x));
            _count = items.Count;

            await PersistItemsLockedAsync(items, cancellationToken);

            foreach (var removedItem in removed)
            {
                foreach (var (_, updater) in _indexes.Values)
                    await updater.OnItemRemovedAsync(removedItem, cancellationToken);
            }

            _logger?.LogDebug("Removed {Count} items from collection '{Name}'", removed.Count, _name);
            return removed.Count;
        }
        finally
        {
            _lock.Release();
        }
    }

    // -----------------------------------------------------------------------
    // UpdateWhereAsync  (NEW METHOD)
    // -----------------------------------------------------------------------
    public async Task<int> UpdateWhereAsync(Func<T, bool> predicate, Action<T> update, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        if (update is null) throw new ArgumentNullException(nameof(update));

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var items = await LoadItemsLockedAsync(cancellationToken);
            int updatedCount = 0;

            foreach (var item in items)
            {
                if (predicate(item))
                {
                    update(item);
                    updatedCount++;
                }
            }

            if (updatedCount == 0)
                return 0;

            await PersistItemsLockedAsync(items, cancellationToken);

            // Rebuild all indexes because keys may have changed after the update.
            // We use the already-loaded `items` snapshot directly to avoid
            // re-acquiring _lock (SemaphoreSlim is not re-entrant).
            foreach (var (_, updater) in _indexes.Values)
                await updater.RebuildAsync(AsAsyncEnumerable(items, cancellationToken), cancellationToken);

            _logger?.LogDebug("Updated {Count} items in collection '{Name}'", updatedCount, _name);
            return updatedCount;
        }
        finally
        {
            _lock.Release();
        }
    }

    // -----------------------------------------------------------------------
    // FirstOrDefaultAsync  (NEW METHOD)
    // -----------------------------------------------------------------------
    public async Task<T?> FirstOrDefaultAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        if (predicate is null) throw new ArgumentNullException(nameof(predicate));

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var items = await LoadItemsLockedAsync(cancellationToken);
            return items.FirstOrDefault(predicate);
        }
        finally
        {
            _lock.Release();
        }
    }

    // -----------------------------------------------------------------------
    // WhereAsync  (NEW METHOD)
    // -----------------------------------------------------------------------
    public async IAsyncEnumerable<T> WhereAsync(
        Func<T, bool> predicate,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        if (predicate is null) throw new ArgumentNullException(nameof(predicate));

        await foreach (var item in GetAllAsync(cancellationToken))
        {
            if (predicate(item))
                yield return item;
        }
    }

    // -----------------------------------------------------------------------
    // GetAllAsync — BUG 2 FIX: hold the lock while reading, then yield outside
    // -----------------------------------------------------------------------
    public async IAsyncEnumerable<T> GetAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        List<T> snapshot;

        // Acquire lock, load a local copy, then release before yielding.
        await _lock.WaitAsync(cancellationToken);
        try
        {
            snapshot = await LoadItemsLockedAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        // Yield outside the lock so we don't hold it during consumer processing.
        foreach (var item in snapshot)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
            yield return item;
            await Task.Yield();
        }
    }

    // -----------------------------------------------------------------------
    // CreateIndexAsync
    // -----------------------------------------------------------------------
    public async Task<IIndex<TKey, T>> CreateIndexAsync<TKey>(
        string name,
        Expression<Func<T, TKey>> keySelector,
        IndexConfiguration? configuration = null,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Index name cannot be null or empty", nameof(name));

        if (_indexes.ContainsKey(name))
            throw new InvalidOperationException($"Index '{name}' already exists");

        try
        {
            var index = new HashIndex<TKey, T>(name, keySelector, configuration, _logger as ILogger<HashIndex<TKey, T>>);
            await index.InitializeAsync(cancellationToken);

            // BUG 1 FIX: store both the raw index and a typed updater.
            _indexes.TryAdd(name, (index, new IndexUpdater<TKey>(index)));

            _logger?.LogDebug("Created index '{IndexName}' on collection '{CollectionName}'", name, _name);
            return index;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create index '{IndexName}' on collection '{CollectionName}'", name, _name);
            throw;
        }
    }

    // -----------------------------------------------------------------------
    // GetIndexAsync — extract index from the tuple
    // -----------------------------------------------------------------------
    public Task<IIndex<TKey, T>?> GetIndexAsync<TKey>(string name, CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        if (_indexes.TryGetValue(name, out var entry) && entry.index is IIndex<TKey, T> typedIndex)
            return Task.FromResult<IIndex<TKey, T>?>(typedIndex);

        return Task.FromResult<IIndex<TKey, T>?>(null);
    }

    // -----------------------------------------------------------------------
    // DropIndexAsync — dispose from the tuple
    // -----------------------------------------------------------------------
    public async Task DropIndexAsync(string name, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        if (_indexes.TryRemove(name, out var entry))
        {
            if (entry.index is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();

            _logger?.LogDebug("Dropped index '{IndexName}' from collection '{CollectionName}'", name, _name);
        }
    }

    // -----------------------------------------------------------------------
    // GetIndexNamesAsync
    // -----------------------------------------------------------------------
    public async IAsyncEnumerable<string> GetIndexNamesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        foreach (var name in _indexes.Keys)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
            yield return name;
            await Task.Yield();
        }
    }

    // -----------------------------------------------------------------------
    // RebuildIndexesAsync — use IRebuildable bridge interface
    // -----------------------------------------------------------------------
    public async Task RebuildIndexesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        try
        {
            _logger?.LogDebug("Rebuilding indexes for collection '{Name}'", _name);

            foreach (var (_, updater) in _indexes.Values)
                await updater.RebuildAsync(GetAllAsync(cancellationToken), cancellationToken);

            _logger?.LogDebug("Completed rebuilding indexes for collection '{Name}'", _name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to rebuild indexes for collection '{Name}'", _name);
            throw;
        }
    }

    // -----------------------------------------------------------------------
    // FlushAsync
    // -----------------------------------------------------------------------
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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

    // -----------------------------------------------------------------------
    // GetStatisticsAsync — BUG 3 FIX: use stored _createdAt / _lastModified
    // -----------------------------------------------------------------------
    public async Task<CollectionStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotInitialized();

        var statistics = new CollectionStatistics
        {
            ElementCount = _count,
            IndexCount = _indexes.Count,
            CreatedAt = _createdAt,       // BUG 3 FIX: real value, not UtcNow
            LastModified = _lastModified  // BUG 3 FIX: real value, not UtcNow
        };

        long storageSize = 0;
        var dataLocation = $"{_name}.data";
        var metadataLocation = $"{_name}.metadata";

        if (await _backend.ExistsAsync(dataLocation, cancellationToken))
            storageSize += await _backend.GetSizeAsync(dataLocation, cancellationToken);

        if (await _backend.ExistsAsync(metadataLocation, cancellationToken))
            storageSize += await _backend.GetSizeAsync(metadataLocation, cancellationToken);

        statistics.StorageSize = storageSize;
        return statistics;
    }

    // -----------------------------------------------------------------------
    // IAsyncEnumerable<T> — delegates to GetAllAsync
    // -----------------------------------------------------------------------
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var item in GetAllAsync(cancellationToken))
            yield return item;
    }

    // -----------------------------------------------------------------------
    // DisposeAsync
    // -----------------------------------------------------------------------
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            foreach (var (index, _) in _indexes.Values)
            {
                if (index is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
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

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads the item list from the backend. Must be called while the caller
    /// holds <see cref="_lock"/>.
    /// </summary>
    private async Task<List<T>> LoadItemsLockedAsync(CancellationToken cancellationToken)
    {
        var location = $"{_name}.data";
        if (!await _backend.ExistsAsync(location, cancellationToken))
            return new List<T>();

        var size = await _backend.GetSizeAsync(location, cancellationToken);
        if (size <= 0)
            return new List<T>();

        var data = await _backend.ReadAsync(location, 0, (int)size, cancellationToken);
        return await DeserializeItemsAsync(data, cancellationToken);
    }

    /// <summary>
    /// Writes the item list to the backend and updates <see cref="_lastModified"/>.
    /// Must be called while the caller holds <see cref="_lock"/>.
    /// </summary>
    private async Task PersistItemsLockedAsync(List<T> items, CancellationToken cancellationToken)
    {
        var location = $"{_name}.data";
        var buffer = new ArrayBufferWriter<byte>();
        await _serializer.SerializeAsync(items, buffer, cancellationToken);
        await _backend.WriteAsync(location, 0, buffer.WrittenMemory, cancellationToken);

        _lastModified = DateTime.UtcNow;
        await SaveMetadataAsync(cancellationToken);
    }

    /// <summary>
    /// BUG 3 FIX: persists metadata.  On the very first save (detected by
    /// whether the metadata file already exists) we lock in <see cref="_createdAt"/>;
    /// subsequent saves preserve the original creation timestamp.
    /// </summary>
    private async Task SaveMetadataAsync(CancellationToken cancellationToken)
    {
        var metaLocation = $"{_name}.metadata";
        bool isFirstSave = !await _backend.ExistsAsync(metaLocation, cancellationToken);

        if (isFirstSave)
            _createdAt = DateTime.UtcNow;   // stamp creation time once

        var metadata = new CollectionMetadata
        {
            Name = _name,
            Count = _count,
            CreatedAt = _createdAt,
            LastModified = _lastModified
        };

        var serializer = new JsonSerializer<CollectionMetadata>();
        var buffer = new ArrayBufferWriter<byte>();
        await serializer.SerializeAsync(metadata, buffer, cancellationToken);
        await _backend.WriteAsync(metaLocation, 0, buffer.WrittenMemory, cancellationToken);
    }

    /// <summary>
    /// BUG 3 FIX: restores <see cref="_createdAt"/> and <see cref="_lastModified"/>
    /// from stored metadata so they survive process restarts.
    /// </summary>
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
            _createdAt = metadata.CreatedAt;       // BUG 3 FIX
            _lastModified = metadata.LastModified; // BUG 3 FIX
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

    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("Collection has not been initialized. Call InitializeAsync() first.");
    }

    /// <summary>
    /// Wraps a plain <see cref="IEnumerable{T}"/> as an <see cref="IAsyncEnumerable{T}"/>
    /// without depending on System.Linq.Async.
    /// </summary>
    private static async IAsyncEnumerable<T> AsAsyncEnumerable(
        IEnumerable<T> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await Task.Yield();
        }
    }

}

// ---------------------------------------------------------------------------
// CollectionMetadata — BUG 3 FIX: CreatedAt is now properly persisted
// ---------------------------------------------------------------------------
/// <summary>
/// Metadata for a persistent collection.
/// </summary>
internal class CollectionMetadata
{
    public string Name { get; set; } = string.Empty;
    public long Count { get; set; }
    public DateTime CreatedAt { get; set; }   // persisted so it survives restarts
    public DateTime LastModified { get; set; }
}
