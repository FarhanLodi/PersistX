using System.Linq.Expressions;

namespace PersistX.Interfaces;

/// <summary>
/// Represents an index for efficient data retrieval.
/// </summary>
/// <typeparam name="TKey">The type of the index key</typeparam>
/// <typeparam name="TValue">The type of the indexed values</typeparam>
public interface IIndex<TKey, TValue> : IAsyncDisposable
{
    /// <summary>
    /// Gets the name of this index.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the index type.
    /// </summary>
    IndexType Type { get; }

    /// <summary>
    /// Gets the key selector expression for this index.
    /// </summary>
    Expression<Func<TValue, TKey>> KeySelector { get; }

    /// <summary>
    /// Adds a value to the index.
    /// </summary>
    /// <param name="key">The key to index</param>
    /// <param name="value">The value to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddAsync(TKey key, TValue value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a value from the index.
    /// </summary>
    /// <param name="key">The key to remove</param>
    /// <param name="value">The value to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveAsync(TKey key, TValue value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a value in the index.
    /// </summary>
    /// <param name="oldKey">The old key</param>
    /// <param name="newKey">The new key</param>
    /// <param name="value">The updated value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateAsync(TKey oldKey, TKey newKey, TValue value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for values by exact key match.
    /// </summary>
    /// <param name="key">The key to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of matching values</returns>
    IAsyncEnumerable<TValue> FindAsync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for values within a range (for ordered indexes).
    /// </summary>
    /// <param name="startKey">Start of the range (inclusive)</param>
    /// <param name="endKey">End of the range (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of values within the range</returns>
    IAsyncEnumerable<TValue> FindRangeAsync(TKey startKey, TKey endKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all keys in the index.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of all keys</returns>
    IAsyncEnumerable<TKey> GetAllKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all values in the index.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of all values</returns>
    IAsyncEnumerable<TValue> GetAllValuesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of items in the index.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of items in the index</returns>
    Task<long> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the index contains a specific key.
    /// </summary>
    /// <param name="key">The key to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the key exists</returns>
    Task<bool> ContainsKeyAsync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all data from the index.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds the index from scratch.
    /// </summary>
    /// <param name="data">The data to rebuild the index from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RebuildAsync(IAsyncEnumerable<TValue> data, CancellationToken cancellationToken = default);
}

/// <summary>
/// Types of indexes supported by PersistX.
/// </summary>
public enum IndexType
{
    /// <summary>
    /// Hash-based index for O(1) lookups.
    /// </summary>
    Hash,

    /// <summary>
    /// B+ tree index for ordered data and range queries.
    /// </summary>
    BTree,

    /// <summary>
    /// Composite index for multiple fields.
    /// </summary>
    Composite,

    /// <summary>
    /// Full-text index for text search.
    /// </summary>
    FullText,

    /// <summary>
    /// Spatial index for geospatial data.
    /// </summary>
    Spatial,

    /// <summary>
    /// Bloom filter for probabilistic membership testing.
    /// </summary>
    BloomFilter
}

/// <summary>
/// Index configuration options.
/// </summary>
public class IndexConfiguration
{
    /// <summary>
    /// Gets or sets whether the index is unique.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Gets or sets whether the index allows null values.
    /// </summary>
    public bool AllowNulls { get; set; } = true;

    /// <summary>
    /// Gets or sets the index fill factor (0.0 to 1.0).
    /// </summary>
    public double FillFactor { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets whether the index is clustered.
    /// </summary>
    public bool IsClustered { get; set; }

    /// <summary>
    /// Gets or sets custom index options.
    /// </summary>
    public Dictionary<string, object> Options { get; set; } = new();
}
