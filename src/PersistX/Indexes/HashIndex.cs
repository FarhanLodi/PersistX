using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;

namespace PersistX.Indexes;

/// <summary>
/// Hash-based index implementation for fast key lookups.
/// </summary>
/// <typeparam name="TKey">The type of the index key</typeparam>
/// <typeparam name="TValue">The type of the indexed values</typeparam>
public class HashIndex<TKey, TValue> : IIndex<TKey, TValue> where TKey : notnull
{
    private readonly ILogger<HashIndex<TKey, TValue>>? _logger;
    private readonly ConcurrentDictionary<TKey, List<TValue>> _index = new();
    private readonly Func<TValue, TKey> _keySelector;
    private bool _disposed;

    public string Name { get; }
    public IndexType Type => IndexType.Hash;
    public Expression<Func<TValue, TKey>> KeySelector { get; }

    public HashIndex(
        string name,
        Expression<Func<TValue, TKey>> keySelector,
        IndexConfiguration? configuration,
        ILogger<HashIndex<TKey, TValue>>? logger = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        KeySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _keySelector = keySelector.Compile();
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Basic initialization - in a real implementation, this might load existing index data
        await Task.CompletedTask;
    }

    public async Task AddAsync(TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _index.AddOrUpdate(key,
            new List<TValue> { value },
            (k, existingList) =>
            {
                existingList.Add(value);
                return existingList;
            });

        await Task.CompletedTask;
    }

    public async Task RemoveAsync(TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_index.TryGetValue(key, out var values))
        {
            values.Remove(value);
            if (values.Count == 0)
            {
                _index.TryRemove(key, out _);
            }
        }

        await Task.CompletedTask;
    }

    public async Task UpdateAsync(TKey oldKey, TKey newKey, TValue value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Remove from old key
        if (_index.TryGetValue(oldKey, out var oldValues))
        {
            oldValues.Remove(value);
            if (oldValues.Count == 0)
            {
                _index.TryRemove(oldKey, out _);
            }
        }

        // Add to new key
        _index.AddOrUpdate(newKey,
            new List<TValue> { value },
            (k, existingList) =>
            {
                existingList.Add(value);
                return existingList;
            });

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<TValue> FindAsync(TKey key, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_index.TryGetValue(key, out var values))
        {
            foreach (var value in values)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;
                    
                yield return value;
                await Task.Yield();
            }
        }
    }

    public async IAsyncEnumerable<TValue> FindRangeAsync(TKey startKey, TKey endKey, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // For hash indexes, range queries are not efficient
        // This is a basic implementation that checks all keys
        var comparer = Comparer<TKey>.Default;
        
        foreach (var kvp in _index)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            if (comparer.Compare(kvp.Key, startKey) >= 0 && comparer.Compare(kvp.Key, endKey) <= 0)
            {
                foreach (var value in kvp.Value)
                {
                    if (cancellationToken.IsCancellationRequested)
                        yield break;
                        
                    yield return value;
                    await Task.Yield();
                }
            }
        }
    }

    public async IAsyncEnumerable<TKey> GetAllKeysAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        foreach (var key in _index.Keys)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            yield return key;
            await Task.Yield();
        }
    }

    public async IAsyncEnumerable<TValue> GetAllValuesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        foreach (var values in _index.Values)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            foreach (var value in values)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;
                    
                yield return value;
                await Task.Yield();
            }
        }
    }

    public async Task<long> GetCountAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await Task.FromResult(_index.Values.Sum(v => v.Count));
    }

    public async Task<bool> ContainsKeyAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await Task.FromResult(_index.ContainsKey(key));
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _index.Clear();
        await Task.CompletedTask;
    }

    public async Task RebuildAsync(IAsyncEnumerable<TValue> data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _index.Clear();

        await foreach (var value in data)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var key = _keySelector(value);
            await AddAsync(key, value, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _index.Clear();
        await Task.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HashIndex<TKey, TValue>));
        }
    }
}
