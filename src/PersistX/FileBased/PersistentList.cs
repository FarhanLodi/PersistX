using System.Text.Json;

namespace PersistX.FileBased;

/// <summary>
/// A simple, easy-to-use persistent list that saves to a JSON file.
/// </summary>
/// <typeparam name="T">The type of elements in the list</typeparam>
public class PersistentList<T>
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates a new persistent list that saves to the specified file path.
    /// </summary>
    /// <param name="filePath">The file path where the list will be saved</param>
    public PersistentList(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Adds an item to the end of the list.
    /// </summary>
    public async Task AddAsync(T item, CancellationToken cancellationToken = default)
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
    /// Adds multiple items to the end of the list.
    /// </summary>
    public async Task AddRangeAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
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
    /// Gets the item at the specified index.
    /// </summary>
    public async Task<T> GetAtAsync(int index, CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        if (index < 0 || index >= list.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return list[index];
    }

    /// <summary>
    /// Sets the item at the specified index.
    /// </summary>
    public async Task SetAtAsync(int index, T item, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            if (index < 0 || index >= list.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            list[index] = item;
            await SaveAsync(list, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Inserts an item at the specified index.
    /// </summary>
    public async Task InsertAsync(int index, T item, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            if (index < 0 || index > list.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            list.Insert(index, item);
            await SaveAsync(list, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Removes the item at the specified index.
    /// </summary>
    public async Task RemoveAtAsync(int index, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            if (index < 0 || index >= list.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            list.RemoveAt(index);
            await SaveAsync(list, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Removes the first occurrence of the specified item.
    /// </summary>
    /// <returns>True if the item was removed, false if not found</returns>
    public async Task<bool> RemoveAsync(T item, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            var removed = list.Remove(item);
            if (removed)
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

    /// <summary>
    /// Removes all items from the list.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await SaveAsync(new List<T>(), cancellationToken);
    }

    /// <summary>
    /// Gets the number of items in the list.
    /// </summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.Count;
    }

    /// <summary>
    /// Checks if the list contains the specified item.
    /// </summary>
    public async Task<bool> ContainsAsync(T item, CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.Contains(item);
    }

    /// <summary>
    /// Gets the index of the first occurrence of the specified item.
    /// </summary>
    /// <returns>The index, or -1 if not found</returns>
    public async Task<int> IndexOfAsync(T item, CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        return list.IndexOf(item);
    }

    /// <summary>
    /// Gets all items in the list.
    /// </summary>
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
    /// Gets a range of items from the list.
    /// </summary>
    public async Task<List<T>> GetRangeAsync(int startIndex, int count, CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken);
        if (startIndex < 0 || startIndex >= list.Count)
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        if (count < 0 || startIndex + count > list.Count)
            throw new ArgumentOutOfRangeException(nameof(count));
        
        return list.GetRange(startIndex, count);
    }

    /// <summary>
    /// Sorts the list using the default comparer.
    /// </summary>
    public async Task SortAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            list.Sort();
            await SaveAsync(list, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Sorts the list using a custom comparer.
    /// </summary>
    public async Task SortAsync(IComparer<T> comparer, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            list.Sort(comparer);
            await SaveAsync(list, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<T>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new List<T>();
        }

        try
        {
            await using var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var result = await JsonSerializer.DeserializeAsync<List<T>>(stream, _jsonOptions, cancellationToken);
            return result ?? new List<T>();
        }
        catch (JsonException)
        {
            // If JSON is corrupted, return empty list
            return new List<T>();
        }
    }

    private async Task SaveAsync(List<T> data, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, data, _jsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
