using System.Text.Json;

namespace PersistX.FileBased;

/// <summary>
/// A simple, easy-to-use persistent set (unique elements) that saves to a JSON file.
/// </summary>
/// <typeparam name="T">The type of elements in the set</typeparam>
public class PersistentSet<T>
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates a new persistent set that saves to the specified file path.
    /// </summary>
    /// <param name="filePath">The file path where the set will be saved</param>
    public PersistentSet(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Adds an element to the set.
    /// </summary>
    /// <returns>True if the element was added (was not already present), false if already exists</returns>
    public async Task<bool> AddAsync(T item, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var set = new HashSet<T>(await LoadAsync(cancellationToken));
            var added = set.Add(item);
            if (added)
            {
                await SaveAsync(set, cancellationToken);
            }
            return added;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Adds multiple elements to the set.
    /// </summary>
    /// <returns>The number of elements that were actually added (not already present)</returns>
    public async Task<int> AddRangeAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var set = new HashSet<T>(await LoadAsync(cancellationToken));
            var beforeCount = set.Count;
            foreach (var item in items)
            {
                set.Add(item);
            }
            var addedCount = set.Count - beforeCount;
            if (addedCount > 0)
            {
                await SaveAsync(set, cancellationToken);
            }
            return addedCount;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Removes an element from the set.
    /// </summary>
    /// <returns>True if the element was removed (was present), false if not found</returns>
    public async Task<bool> RemoveAsync(T item, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var set = new HashSet<T>(await LoadAsync(cancellationToken));
            var removed = set.Remove(item);
            if (removed)
            {
                await SaveAsync(set, cancellationToken);
            }
            return removed;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Checks if the set contains the specified element.
    /// </summary>
    public async Task<bool> ContainsAsync(T item, CancellationToken cancellationToken = default)
    {
        var set = new HashSet<T>(await LoadAsync(cancellationToken));
        return set.Contains(item);
    }

    /// <summary>
    /// Gets the number of elements in the set.
    /// </summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var set = await LoadAsync(cancellationToken);
        return set.Count;
    }

    /// <summary>
    /// Gets all elements in the set.
    /// </summary>
    public async IAsyncEnumerable<T> GetAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var set = await LoadAsync(cancellationToken);
        foreach (var item in set)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
            yield return item;
            await Task.Yield();
        }
    }

    /// <summary>
    /// Removes all elements from the set.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await SaveAsync(new HashSet<T>(), cancellationToken);
    }

    /// <summary>
    /// Checks if this set is a subset of another set.
    /// </summary>
    public async Task<bool> IsSubsetOfAsync(PersistentSet<T> other, CancellationToken cancellationToken = default)
    {
        var thisSet = new HashSet<T>(await LoadAsync(cancellationToken));
        var otherSet = new HashSet<T>(await other.LoadAsync(cancellationToken));
        return thisSet.IsSubsetOf(otherSet);
    }

    /// <summary>
    /// Checks if this set is a superset of another set.
    /// </summary>
    public async Task<bool> IsSupersetOfAsync(PersistentSet<T> other, CancellationToken cancellationToken = default)
    {
        var thisSet = new HashSet<T>(await LoadAsync(cancellationToken));
        var otherSet = new HashSet<T>(await other.LoadAsync(cancellationToken));
        return thisSet.IsSupersetOf(otherSet);
    }

    /// <summary>
    /// Checks if this set overlaps with another set (has common elements).
    /// </summary>
    public async Task<bool> OverlapsAsync(PersistentSet<T> other, CancellationToken cancellationToken = default)
    {
        var thisSet = new HashSet<T>(await LoadAsync(cancellationToken));
        var otherSet = new HashSet<T>(await other.LoadAsync(cancellationToken));
        return thisSet.Overlaps(otherSet);
    }

    /// <summary>
    /// Checks if this set is equal to another set.
    /// </summary>
    public async Task<bool> SetEqualsAsync(PersistentSet<T> other, CancellationToken cancellationToken = default)
    {
        var thisSet = new HashSet<T>(await LoadAsync(cancellationToken));
        var otherSet = new HashSet<T>(await other.LoadAsync(cancellationToken));
        return thisSet.SetEquals(otherSet);
    }

    /// <summary>
    /// Creates a new set that is the union of this set and another set.
    /// </summary>
    public async Task<PersistentSet<T>> UnionWithAsync(PersistentSet<T> other, CancellationToken cancellationToken = default)
    {
        var thisSet = new HashSet<T>(await LoadAsync(cancellationToken));
        var otherSet = new HashSet<T>(await other.LoadAsync(cancellationToken));
        var unionSet = new HashSet<T>(thisSet);
        unionSet.UnionWith(otherSet);
        
        var tempPath = Path.GetTempFileName();
        var result = new PersistentSet<T>(tempPath);
        await result.SaveAsync(unionSet, cancellationToken);
        return result;
    }

    /// <summary>
    /// Creates a new set that is the intersection of this set and another set.
    /// </summary>
    public async Task<PersistentSet<T>> IntersectWithAsync(PersistentSet<T> other, CancellationToken cancellationToken = default)
    {
        var thisSet = new HashSet<T>(await LoadAsync(cancellationToken));
        var otherSet = new HashSet<T>(await other.LoadAsync(cancellationToken));
        var intersectSet = new HashSet<T>(thisSet);
        intersectSet.IntersectWith(otherSet);
        
        var tempPath = Path.GetTempFileName();
        var result = new PersistentSet<T>(tempPath);
        await result.SaveAsync(intersectSet, cancellationToken);
        return result;
    }

    /// <summary>
    /// Creates a new set that is the difference of this set and another set.
    /// </summary>
    public async Task<PersistentSet<T>> ExceptWithAsync(PersistentSet<T> other, CancellationToken cancellationToken = default)
    {
        var thisSet = new HashSet<T>(await LoadAsync(cancellationToken));
        var otherSet = new HashSet<T>(await other.LoadAsync(cancellationToken));
        var exceptSet = new HashSet<T>(thisSet);
        exceptSet.ExceptWith(otherSet);
        
        var tempPath = Path.GetTempFileName();
        var result = new PersistentSet<T>(tempPath);
        await result.SaveAsync(exceptSet, cancellationToken);
        return result;
    }

    internal async Task<HashSet<T>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new HashSet<T>();
        }

        try
        {
            await using var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var result = await JsonSerializer.DeserializeAsync<HashSet<T>>(stream, _jsonOptions, cancellationToken);
            return result ?? new HashSet<T>();
        }
        catch (JsonException)
        {
            // If JSON is corrupted, return empty set
            return new HashSet<T>();
        }
    }

    internal async Task SaveAsync(HashSet<T> data, CancellationToken cancellationToken)
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
