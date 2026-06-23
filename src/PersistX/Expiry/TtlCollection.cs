using System.Text.Json;

namespace PersistX.Expiry;

/// <summary>
/// A persistent TTL (Time-To-Live) collection. Items automatically expire after their TTL.
/// Backed by a JSON file. Thread-safe.
/// </summary>
public sealed class TtlCollection<T>
{
    private readonly string _filePath;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<TtlItem<T>>? _cache;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TtlCollection(string filePath, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Add item with a TTL duration.</summary>
    public async Task AddAsync(T item, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var expiresAt = _timeProvider.GetUtcNow().Add(ttl);
        await AddAsync(item, expiresAt, cancellationToken);
    }

    /// <summary>Add item with explicit expiry time.</summary>
    public async Task AddAsync(T item, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            list.Add(new TtlItem<T>
            {
                Value = item,
                ExpiresAt = expiresAt,
                CreatedAt = _timeProvider.GetUtcNow()
            });
            await SaveAsync(list, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Get first non-expired item matching predicate. Returns null if not found or expired.</summary>
    public async Task<T?> GetAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        var match = list.FirstOrDefault(x => !x.IsExpired(_timeProvider) && predicate(x.Value));
        return match is not null ? match.Value : default;
    }

    /// <summary>Get all non-expired items.</summary>
    public async Task<List<T>> GetAllValidAsync(CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.Where(x => !x.IsExpired(_timeProvider)).Select(x => x.Value).ToList();
    }

    /// <summary>Remove all expired items. Returns count removed.</summary>
    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Load raw from disk (bypass cache to avoid double-lock issues)
            var list = await LoadRawAsync(cancellationToken);
            var before = list.Count;
            list.RemoveAll(x => x.IsExpired(_timeProvider));
            var removed = before - list.Count;
            if (removed > 0)
            {
                await SaveAsync(list, cancellationToken);
            }
            return removed;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Remove first item matching predicate. Returns true if removed.</summary>
    public async Task<bool> RemoveAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            var index = list.FindIndex(x => predicate(x.Value));
            if (index < 0)
                return false;

            list.RemoveAt(index);
            await SaveAsync(list, cancellationToken);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Count of non-expired items.</summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.Count(x => !x.IsExpired(_timeProvider));
    }

    /// <summary>Count of all items including expired.</summary>
    public async Task<int> TotalCountAsync(CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.Count;
    }

    /// <summary>Remove all items.</summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await SaveAsync(new List<TtlItem<T>>(), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Enumerate all non-expired items.</summary>
    public async IAsyncEnumerable<T> GetAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        foreach (var item in list)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
            if (!item.IsExpired(_timeProvider))
            {
                yield return item.Value;
                await Task.Yield();
            }
        }
    }

    /// <summary>Get item with its TTL metadata.</summary>
    public async Task<TtlItem<T>?> GetWithMetaAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.FirstOrDefault(x => !x.IsExpired(_timeProvider) && predicate(x.Value));
    }

    // Private helpers

    /// <summary>
    /// Returns the cached list if available; otherwise loads from disk and auto-purges expired items.
    /// </summary>
    private async Task<List<TtlItem<T>>> LoadAsync(CancellationToken ct)
    {
        if (_cache != null)
            return _cache;

        var list = await LoadRawAsync(ct);

        // Lazy expiry: remove expired items on every load
        var before = list.Count;
        list.RemoveAll(x => x.IsExpired(_timeProvider));
        if (list.Count != before)
        {
            // Persist the purged state; save directly without re-entering lock
            await PersistAsync(list, ct);
        }

        _cache = list;
        return _cache;
    }

    /// <summary>Reads the raw list from disk without purging or caching.</summary>
    private async Task<List<TtlItem<T>>> LoadRawAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
            return new List<TtlItem<T>>();

        try
        {
            await using var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var result = await JsonSerializer.DeserializeAsync<List<TtlItem<T>>>(stream, _jsonOptions, ct);
            return result ?? new List<TtlItem<T>>();
        }
        catch (JsonException)
        {
            // Corrupted file — treat as empty
            return new List<TtlItem<T>>();
        }
    }

    /// <summary>Saves data to the file and updates the cache.</summary>
    private async Task SaveAsync(List<TtlItem<T>> data, CancellationToken ct)
    {
        await PersistAsync(data, ct);
        _cache = data;
    }

    /// <summary>Writes data to disk without touching the cache.</summary>
    private async Task PersistAsync(List<TtlItem<T>> data, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, data, _jsonOptions, ct);
        await stream.FlushAsync(ct);
    }
}
