using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;

namespace PersistX.Indexes;

/// <summary>
/// A hash-based index that persists its data to the storage backend and survives application restarts.
/// </summary>
/// <typeparam name="TKey">The type of the index key.</typeparam>
/// <typeparam name="TValue">The type of the indexed values.</typeparam>
public class PersistentHashIndex<TKey, TValue> : IIndex<TKey, TValue> where TKey : notnull
{
    private readonly IBackend _backend;
    private readonly string _storageLocation;
    private readonly Dictionary<TKey, List<TValue>> _index = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Func<TValue, TKey> _compiledKeySelector;
    private readonly ILogger<PersistentHashIndex<TKey, TValue>>? _logger;
    private bool _disposed;

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IndexType Type => IndexType.Hash;

    /// <inheritdoc />
    public Expression<Func<TValue, TKey>> KeySelector { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="PersistentHashIndex{TKey, TValue}"/>.
    /// </summary>
    /// <param name="name">The name of this index. Also determines the storage file name (<c>{name}.hidx</c>).</param>
    /// <param name="keySelector">An expression that selects the index key from a value.</param>
    /// <param name="backend">The storage backend used to persist this index.</param>
    /// <param name="config">Optional index configuration.</param>
    /// <param name="logger">Optional logger.</param>
    public PersistentHashIndex(
        string name,
        Expression<Func<TValue, TKey>> keySelector,
        IBackend backend,
        IndexConfiguration? config = null,
        ILogger<PersistentHashIndex<TKey, TValue>>? logger = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        KeySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _compiledKeySelector = keySelector.Compile();
        _storageLocation = $"{name}.hidx";
        _logger = logger;
    }

    /// <summary>
    /// Loads the index from the backend if a persisted snapshot exists.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!await _backend.ExistsAsync(_storageLocation, ct).ConfigureAwait(false))
            {
                _logger?.LogDebug("No existing snapshot found for index '{Name}'. Starting empty.", Name);
                return;
            }

            var size = await _backend.GetSizeAsync(_storageLocation, ct).ConfigureAwait(false);
            if (size <= 0)
                return;

            var bytes = await _backend.ReadAsync(_storageLocation, 0, (int)size, ct).ConfigureAwait(false);
            var deserialized = JsonSerializer.Deserialize<Dictionary<TKey, List<TValue>>>(bytes.Span);

            if (deserialized is not null)
            {
                _index.Clear();
                foreach (var kvp in deserialized)
                    _index[kvp.Key] = kvp.Value;

                _logger?.LogDebug(
                    "Index '{Name}' loaded {Count} key(s) from persisted snapshot.",
                    Name,
                    _index.Count);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to deserialize persisted snapshot for index '{Name}'. Starting empty.", Name);
            _index.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task AddAsync(TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_index.TryGetValue(key, out var list))
            {
                list = new List<TValue>();
                _index[key] = list;
            }
            list.Add(value);

            await PersistAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_index.TryGetValue(key, out var list))
            {
                list.Remove(value);
                if (list.Count == 0)
                    _index.Remove(key);

                await PersistAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TKey oldKey, TKey newKey, TValue value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Remove from old key
            if (_index.TryGetValue(oldKey, out var oldList))
            {
                oldList.Remove(value);
                if (oldList.Count == 0)
                    _index.Remove(oldKey);
            }

            // Add to new key
            if (!_index.TryGetValue(newKey, out var newList))
            {
                newList = new List<TValue>();
                _index[newKey] = newList;
            }
            newList.Add(value);

            await PersistAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TValue> FindAsync(
        TKey key,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        List<TValue> snapshot;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            snapshot = _index.TryGetValue(key, out var list)
                ? new List<TValue>(list)
                : new List<TValue>();
        }
        finally
        {
            _lock.Release();
        }

        foreach (var value in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return value;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    /// <remarks>Hash indexes do not maintain order; this performs an O(n) scan over all keys.</remarks>
    public async IAsyncEnumerable<TValue> FindRangeAsync(
        TKey startKey,
        TKey endKey,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        List<(TKey Key, List<TValue> Values)> snapshot;
        var comparer = Comparer<TKey>.Default;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            snapshot = _index
                .Where(kvp =>
                    comparer.Compare(kvp.Key, startKey) >= 0 &&
                    comparer.Compare(kvp.Key, endKey) <= 0)
                .Select(kvp => (kvp.Key, new List<TValue>(kvp.Value)))
                .ToList();
        }
        finally
        {
            _lock.Release();
        }

        foreach (var (_, values) in snapshot)
        {
            foreach (var value in values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return value;
                await Task.Yield();
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TKey> GetAllKeysAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        List<TKey> snapshot;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            snapshot = new List<TKey>(_index.Keys);
        }
        finally
        {
            _lock.Release();
        }

        foreach (var key in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return key;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TValue> GetAllValuesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        List<TValue> snapshot;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            snapshot = _index.Values.SelectMany(v => v).ToList();
        }
        finally
        {
            _lock.Release();
        }

        foreach (var value in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return value;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public async Task<long> GetCountAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _index.Values.Sum(v => (long)v.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> ContainsKeyAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _index.ContainsKey(key);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _index.Clear();
            await _backend.DeleteAsync(_storageLocation, cancellationToken).ConfigureAwait(false);
            _logger?.LogDebug("Index '{Name}' cleared and backing store deleted.", Name);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RebuildAsync(IAsyncEnumerable<TValue> data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(data);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _index.Clear();

            await foreach (var value in data.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                var key = _compiledKeySelector(value);
                if (!_index.TryGetValue(key, out var list))
                {
                    list = new List<TValue>();
                    _index[key] = list;
                }
                list.Add(value);
            }

            await PersistAsync(cancellationToken).ConfigureAwait(false);

            _logger?.LogDebug("Index '{Name}' rebuilt with {Count} key(s).", Name, _index.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _index.Clear();
        _lock.Dispose();

        await Task.CompletedTask;
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Serializes the current in-memory index to the backend.
    /// Must be called while <see cref="_lock"/> is already held.
    /// </summary>
    private async Task PersistAsync(CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(_index);
        await _backend.WriteAsync(_storageLocation, 0, bytes, ct).ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PersistentHashIndex<TKey, TValue>));
    }
}
