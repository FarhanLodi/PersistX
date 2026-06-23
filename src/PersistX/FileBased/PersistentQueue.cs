using System.Text.Json;

namespace PersistX.FileBased;

/// <summary>
/// A persistent FIFO queue backed by a JSON file. Thread-safe.
/// </summary>
/// <typeparam name="T">The type of elements in the queue</typeparam>
public class PersistentQueue<T>
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private List<T>? _cache = null;

    /// <summary>
    /// Creates a new persistent queue that saves to the specified file path.
    /// </summary>
    /// <param name="filePath">The file path where the queue will be saved</param>
    public PersistentQueue(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Adds an item to the back of the queue.
    /// </summary>
    /// <param name="item">The item to enqueue</param>
    /// <param name="cancellationToken">A cancellation token</param>
    public async Task EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            list.Add(item);
            await SaveAsync(list, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Adds multiple items to the back of the queue in enumeration order.
    /// </summary>
    /// <param name="items">The items to enqueue</param>
    /// <param name="cancellationToken">A cancellation token</param>
    public async Task EnqueueRangeAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            list.AddRange(items);
            await SaveAsync(list, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Removes and returns the item at the front of the queue.
    /// Returns <see langword="default"/> if the queue is empty.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>The dequeued item, or <see langword="default"/> if the queue is empty</returns>
    public async Task<T?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            if (list.Count == 0)
                return default;

            var item = list[0];
            list.RemoveAt(0);
            await SaveAsync(list, cancellationToken);
            return item;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the item at the front of the queue without removing it.
    /// Returns <see langword="default"/> if the queue is empty.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>The front item, or <see langword="default"/> if the queue is empty</returns>
    public async Task<T?> PeekAsync(CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.Count == 0 ? default : list[0];
    }

    /// <summary>
    /// Gets the number of items in the queue.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>The number of items currently in the queue</returns>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.Count;
    }

    /// <summary>
    /// Determines whether the queue contains the specified item.
    /// </summary>
    /// <param name="item">The item to locate</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns><see langword="true"/> if the item is found; otherwise <see langword="false"/></returns>
    public async Task<bool> ContainsAsync(T item, CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.Contains(item);
    }

    /// <summary>
    /// Removes all items from the queue.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await SaveAsync(new List<T>(), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Enumerates all items in the queue from front to back without removing them.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>An async enumerable of all items, ordered front to back</returns>
    public async IAsyncEnumerable<T> GetAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        foreach (var item in list)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
            yield return item;
            await Task.Yield();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the queue contains no items.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns><see langword="true"/> if the queue is empty; otherwise <see langword="false"/></returns>
    public async Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.Count == 0;
    }

    /// <summary>
    /// Attempts to remove and return the item at the front of the queue.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>
    /// A tuple where <c>success</c> is <see langword="true"/> and <c>item</c> is the dequeued value
    /// if the queue was non-empty; otherwise <c>success</c> is <see langword="false"/> and
    /// <c>item</c> is <see langword="default"/>.
    /// </returns>
    public async Task<(bool success, T? item)> TryDequeueAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            if (list.Count == 0)
                return (false, default);

            var item = list[0];
            list.RemoveAt(0);
            await SaveAsync(list, cancellationToken);
            return (true, item);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<T>> LoadAsync(CancellationToken ct)
    {
        if (_cache is not null)
            return _cache;

        if (!File.Exists(_filePath))
            return new List<T>();

        try
        {
            await using var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var result = await JsonSerializer.DeserializeAsync<List<T>>(stream, _jsonOptions, ct);
            return result ?? new List<T>();
        }
        catch (JsonException)
        {
            // If JSON is corrupted, return empty list
            return new List<T>();
        }
    }

    private async Task SaveAsync(List<T> data, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, data, _jsonOptions, ct);
        await stream.FlushAsync(ct);
        _cache = data;
    }
}
