using System.Text.Json;

namespace PersistX.FileBased;

/// <summary>
/// A persistent LIFO stack backed by a JSON file. Thread-safe.
/// </summary>
/// <typeparam name="T">The type of elements in the stack</typeparam>
public class PersistentStack<T>
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
    /// Creates a new persistent stack that saves to the specified file path.
    /// </summary>
    /// <param name="filePath">The file path where the stack will be saved</param>
    public PersistentStack(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Pushes an item onto the top of the stack.
    /// </summary>
    /// <param name="item">The item to push</param>
    /// <param name="cancellationToken">A cancellation token</param>
    public async Task PushAsync(T item, CancellationToken cancellationToken = default)
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
    /// Pushes multiple items onto the stack in enumeration order so that the last item
    /// in the enumerable ends up on top.
    /// </summary>
    /// <param name="items">The items to push</param>
    /// <param name="cancellationToken">A cancellation token</param>
    public async Task PushRangeAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
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
    /// Removes and returns the item on the top of the stack.
    /// Returns <see langword="default"/> if the stack is empty.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>The popped item, or <see langword="default"/> if the stack is empty</returns>
    public async Task<T?> PopAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            if (list.Count == 0)
                return default;

            var lastIndex = list.Count - 1;
            var item = list[lastIndex];
            list.RemoveAt(lastIndex);
            await SaveAsync(list, cancellationToken);
            return item;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the item on the top of the stack without removing it.
    /// Returns <see langword="default"/> if the stack is empty.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>The top item, or <see langword="default"/> if the stack is empty</returns>
    public async Task<T?> PeekAsync(CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.Count == 0 ? default : list[list.Count - 1];
    }

    /// <summary>
    /// Gets the number of items in the stack.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>The number of items currently in the stack</returns>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.Count;
    }

    /// <summary>
    /// Determines whether the stack contains the specified item.
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
    /// Removes all items from the stack.
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
    /// Enumerates all items in the stack from top to bottom without removing them.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>An async enumerable of all items, ordered top to bottom (LIFO order)</returns>
    public async IAsyncEnumerable<T> GetAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
            yield return list[i];
            await Task.Yield();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the stack contains no items.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns><see langword="true"/> if the stack is empty; otherwise <see langword="false"/></returns>
    public async Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.Count == 0;
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
