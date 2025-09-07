using System.Collections;
using System.Linq.Expressions;

namespace PersistX.Interfaces;

/// <summary>
/// Base interface for all persistent collections.
/// </summary>
/// <typeparam name="T">The type of elements in the collection</typeparam>
public interface IPersistentCollection<T> : IAsyncDisposable, IAsyncEnumerable<T>
{
    /// <summary>
    /// Gets the name of this collection.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the number of elements in the collection.
    /// </summary>
    Task<long> CountAsync { get; }

    /// <summary>
    /// Gets whether the collection is read-only.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Gets the serializer used by this collection.
    /// </summary>
    ISerializer<T> Serializer { get; }

    /// <summary>
    /// Clears all elements from the collection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the collection contains a specific element.
    /// </summary>
    /// <param name="item">The element to check for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the element exists</returns>
    Task<bool> ContainsAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an element to the collection.
    /// </summary>
    /// <param name="item">The element to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple elements to the collection in a single operation.
    /// </summary>
    /// <param name="items">The elements to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddRangeAsync(IEnumerable<T> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all elements in the collection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of all elements</returns>
    IAsyncEnumerable<T> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an index on this collection.
    /// </summary>
    /// <typeparam name="TKey">The type of the index key</typeparam>
    /// <param name="name">The name of the index</param>
    /// <param name="keySelector">Expression to select the key from elements</param>
    /// <param name="configuration">Index configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created index</returns>
    Task<IIndex<TKey, T>> CreateIndexAsync<TKey>(
        string name,
        Expression<Func<T, TKey>> keySelector,
        IndexConfiguration? configuration = null,
        CancellationToken cancellationToken = default) where TKey : notnull;

    /// <summary>
    /// Gets an existing index by name.
    /// </summary>
    /// <typeparam name="TKey">The type of the index key</typeparam>
    /// <param name="name">The name of the index</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The index, or null if not found</returns>
    Task<IIndex<TKey, T>?> GetIndexAsync<TKey>(string name, CancellationToken cancellationToken = default) where TKey : notnull;

    /// <summary>
    /// Drops an index by name.
    /// </summary>
    /// <param name="name">The name of the index to drop</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DropIndexAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all indexes on this collection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of index names</returns>
    IAsyncEnumerable<string> GetIndexNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds all indexes on this collection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RebuildIndexesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any pending changes to persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets collection statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection statistics</returns>
    Task<CollectionStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about a persistent collection.
/// </summary>
public class CollectionStatistics
{
    /// <summary>
    /// Gets or sets the total number of elements.
    /// </summary>
    public long ElementCount { get; set; }

    /// <summary>
    /// Gets or sets the total storage size in bytes.
    /// </summary>
    public long StorageSize { get; set; }

    /// <summary>
    /// Gets or sets the number of indexes.
    /// </summary>
    public int IndexCount { get; set; }

    /// <summary>
    /// Gets or sets the last modified timestamp.
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets additional custom statistics.
    /// </summary>
    public Dictionary<string, object> CustomStats { get; set; } = new();
}
