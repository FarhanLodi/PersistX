using System.Collections;
using PersistX.Enums;

namespace PersistX.Interfaces;

/// <summary>
/// Represents a database transaction with ACID properties.
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique transaction identifier.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the transaction isolation level.
    /// </summary>
    IsolationLevel IsolationLevel { get; }

    /// <summary>
    /// Gets the current transaction state.
    /// </summary>
    TransactionState State { get; }

    /// <summary>
    /// Commits the transaction, making all changes permanent.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the transaction, discarding all changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a savepoint within the transaction.
    /// </summary>
    /// <param name="name">Savepoint name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Savepoint object</returns>
    Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back to a specific savepoint.
    /// </summary>
    /// <param name="savepoint">Savepoint to rollback to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RollbackToSavepointAsync(ISavepoint savepoint, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a savepoint within a transaction.
/// </summary>
public interface ISavepoint
{
    /// <summary>
    /// Gets the savepoint name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the transaction this savepoint belongs to.
    /// </summary>
    ITransaction Transaction { get; }

    /// <summary>
    /// Gets the timestamp when the savepoint was created.
    /// </summary>
    DateTime CreatedAt { get; }
}

