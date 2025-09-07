using System.Text.Json;

namespace PersistX.FileBased;

/// <summary>
/// A simple, easy-to-use persistent dictionary that saves to a JSON file.
/// </summary>
/// <typeparam name="TKey">The type of keys</typeparam>
/// <typeparam name="TValue">The type of values</typeparam>
public class PersistentDictionary<TKey, TValue> where TKey : notnull
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates a new persistent dictionary that saves to the specified file path.
    /// </summary>
    /// <param name="filePath">The file path where the dictionary will be saved</param>
    public PersistentDictionary(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Sets the value for the specified key (adds if key doesn't exist, updates if it does).
    /// </summary>
    public async Task SetAsync(TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var dict = await LoadAsync(cancellationToken);
            dict[key] = value;
            await SaveAsync(dict, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Adds a key-value pair to the dictionary (throws if key already exists).
    /// </summary>
    public async Task AddAsync(TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var dict = await LoadAsync(cancellationToken);
            if (dict.ContainsKey(key))
                throw new ArgumentException($"Key '{key}' already exists", nameof(key));
            dict.Add(key, value);
            await SaveAsync(dict, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Adds multiple key-value pairs to the dictionary.
    /// </summary>
    public async Task AddRangeAsync(IEnumerable<KeyValuePair<TKey, TValue>> items, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var dict = await LoadAsync(cancellationToken);
            foreach (var kvp in items)
            {
                dict[kvp.Key] = kvp.Value;
            }
            await SaveAsync(dict, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets the value for the specified key.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found</exception>
    public async Task<TValue> GetAsync(TKey key, CancellationToken cancellationToken = default)
    {
        var dict = await LoadAsync(cancellationToken);
        if (!dict.TryGetValue(key, out var value))
            throw new KeyNotFoundException($"Key '{key}' not found");
        return value;
    }

    /// <summary>
    /// Tries to get the value for the specified key.
    /// </summary>
    /// <returns>A tuple with (found, value) where found is true if the key exists</returns>
    public async Task<(bool found, TValue? value)> TryGetAsync(TKey key, CancellationToken cancellationToken = default)
    {
        var dict = await LoadAsync(cancellationToken);
        return dict.TryGetValue(key, out var value) ? (true, value) : (false, default);
    }

    /// <summary>
    /// Gets the value for the specified key, or returns the default value if not found.
    /// </summary>
    public async Task<TValue> GetOrDefaultAsync(TKey key, TValue defaultValue, CancellationToken cancellationToken = default)
    {
        var dict = await LoadAsync(cancellationToken);
        return dict.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Removes the key-value pair with the specified key.
    /// </summary>
    /// <returns>True if the key was removed, false if not found</returns>
    public async Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var dict = await LoadAsync(cancellationToken);
            var removed = dict.Remove(key);
            if (removed)
            {
                await SaveAsync(dict, cancellationToken);
            }
            return removed;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Checks if the dictionary contains the specified key.
    /// </summary>
    public async Task<bool> ContainsKeyAsync(TKey key, CancellationToken cancellationToken = default)
    {
        var dict = await LoadAsync(cancellationToken);
        return dict.ContainsKey(key);
    }

    /// <summary>
    /// Checks if the dictionary contains the specified value.
    /// </summary>
    public async Task<bool> ContainsValueAsync(TValue value, CancellationToken cancellationToken = default)
    {
        var dict = await LoadAsync(cancellationToken);
        return dict.ContainsValue(value);
    }

    /// <summary>
    /// Gets the number of key-value pairs in the dictionary.
    /// </summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var dict = await LoadAsync(cancellationToken);
        return dict.Count;
    }

    /// <summary>
    /// Gets all key-value pairs in the dictionary.
    /// </summary>
    public async IAsyncEnumerable<KeyValuePair<TKey, TValue>> GetAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var dict = await LoadAsync(cancellationToken);
        foreach (var kvp in dict)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
            yield return kvp;
            await Task.Yield();
        }
    }

    /// <summary>
    /// Gets all keys in the dictionary.
    /// </summary>
    public async IAsyncEnumerable<TKey> GetKeysAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var dict = await LoadAsync(cancellationToken);
        foreach (var key in dict.Keys)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
            yield return key;
            await Task.Yield();
        }
    }

    /// <summary>
    /// Gets all values in the dictionary.
    /// </summary>
    public async IAsyncEnumerable<TValue> GetValuesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var dict = await LoadAsync(cancellationToken);
        foreach (var value in dict.Values)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
            yield return value;
            await Task.Yield();
        }
    }

    /// <summary>
    /// Removes all key-value pairs from the dictionary.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await SaveAsync(new Dictionary<TKey, TValue>(), cancellationToken);
    }

    private async Task<Dictionary<TKey, TValue>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<TKey, TValue>();
        }

        try
        {
            await using var stream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var result = await JsonSerializer.DeserializeAsync<Dictionary<TKey, TValue>>(stream, _jsonOptions, cancellationToken);
            return result ?? new Dictionary<TKey, TValue>();
        }
        catch (JsonException)
        {
            // If JSON is corrupted, return empty dictionary
            return new Dictionary<TKey, TValue>();
        }
    }

    private async Task SaveAsync(Dictionary<TKey, TValue> data, CancellationToken cancellationToken)
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
