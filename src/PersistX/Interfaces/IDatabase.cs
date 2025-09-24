using PersistX.Enums;
using PersistX.Models;

namespace PersistX.Interfaces;

/// <summary>
/// Represents a PersistX database instance.
/// </summary>
public interface IDatabase : IAsyncDisposable
{
    /// <summary>
    /// Gets the name of this database.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the backend used by this database.
    /// </summary>
    IBackend Backend { get; }

    /// <summary>
    /// Gets the transaction manager for this database.
    /// </summary>
    ITransactionManager TransactionManager { get; }

    /// <summary>
    /// Initializes the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new persistent collection.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection</typeparam>
    /// <param name="name">The name of the collection</param>
    /// <param name="serializer">Optional custom serializer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created collection</returns>
    Task<IPersistentCollection<T>> CreateCollectionAsync<T>(
        string name,
        ISerializer<List<T>>? serializer = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an existing persistent collection.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection</typeparam>
    /// <param name="name">The name of the collection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The collection, or null if not found</returns>
    Task<IPersistentCollection<T>?> GetCollectionAsync<T>(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops a collection.
    /// </summary>
    /// <param name="name">The name of the collection to drop</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DropCollectionAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all collection names in this database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of collection names</returns>
    IAsyncEnumerable<string> GetCollectionNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <param name="isolationLevel">Transaction isolation level</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The new transaction</returns>
    Task<ITransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a function within a transaction.
    /// </summary>
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="func">The function to execute</param>
    /// <param name="isolationLevel">Transaction isolation level</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the function</returns>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<ITransaction, Task<TResult>> func,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an action within a transaction.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <param name="isolationLevel">Transaction isolation level</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExecuteInTransactionAsync(
        Func<ITransaction, Task> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes all pending changes to persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets database statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Database statistics</returns>
    Task<DatabaseStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs database maintenance operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MaintenanceAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages transactions for a database.
/// </summary>
public interface ITransactionManager
{
    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <param name="isolationLevel">Transaction isolation level</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The new transaction</returns>
    Task<ITransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current active transaction, if any.
    /// </summary>
    /// <returns>The current transaction, or null if none</returns>
    ITransaction? GetCurrentTransaction();

    /// <summary>
    /// Gets all active transactions.
    /// </summary>
    /// <returns>Collection of active transactions</returns>
    IReadOnlyList<ITransaction> GetActiveTransactions();

    /// <summary>
    /// Detects and resolves deadlocks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DetectAndResolveDeadlocksAsync(CancellationToken cancellationToken = default);
}

