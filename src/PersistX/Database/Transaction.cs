using Microsoft.Extensions.Logging;
using PersistX.Enums;
using PersistX.Interfaces;

namespace PersistX.Database;

/// <summary>
/// Basic transaction implementation.
/// </summary>
internal class Transaction : ITransaction
{
    private readonly ILogger<Transaction>? _logger;
    private readonly List<ISavepoint> _savepoints = new();
    private readonly Action<Guid>? _onTransactionCompleted;
    private TransactionState _state = TransactionState.Active;

    /// <summary>
    /// Gets the unique transaction identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the transaction isolation level.
    /// </summary>
    public IsolationLevel IsolationLevel { get; }

    /// <summary>
    /// Gets the current transaction state.
    /// </summary>
    public TransactionState State => _state;

    /// <summary>
    /// Gets the timestamp when the transaction was created.
    /// </summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the Transaction class.
    /// </summary>
    /// <param name="id">Transaction identifier</param>
    /// <param name="isolationLevel">Transaction isolation level</param>
    /// <param name="logger">Optional logger</param>
    /// <param name="onTransactionCompleted">Optional callback when transaction completes</param>
    public Transaction(Guid id, IsolationLevel isolationLevel, ILogger<Transaction>? logger = null, Action<Guid>? onTransactionCompleted = null)
    {
        Id = id;
        IsolationLevel = isolationLevel;
        _logger = logger;
        _onTransactionCompleted = onTransactionCompleted;
    }

    /// <summary>
    /// Commits the transaction, making all changes permanent.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_state != TransactionState.Active)
        {
            throw new InvalidOperationException($"Cannot commit transaction in state {_state}");
        }

        try
        {
            _state = TransactionState.Committing;
            _logger?.LogDebug("Committing transaction {TransactionId}", Id);

            // In a real implementation, this would commit all changes to storage
            // For now, we'll just simulate the commit

            _state = TransactionState.Committed;
            _logger?.LogDebug("Transaction {TransactionId} committed successfully", Id);

            // Notify that transaction is completed
            _onTransactionCompleted?.Invoke(Id);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _state = TransactionState.Aborted;
            _logger?.LogError(ex, "Failed to commit transaction {TransactionId}", Id);
            
            // Notify that transaction is completed (aborted)
            _onTransactionCompleted?.Invoke(Id);
            
            throw;
        }
    }

    /// <summary>
    /// Rolls back the transaction, discarding all changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_state != TransactionState.Active && _state != TransactionState.Committing)
        {
            throw new InvalidOperationException($"Cannot rollback transaction in state {_state}");
        }

        try
        {
            _state = TransactionState.RollingBack;
            _logger?.LogDebug("Rolling back transaction {TransactionId}", Id);

            // In a real implementation, this would rollback all changes
            // For now, we'll just simulate the rollback

            _state = TransactionState.RolledBack;
            _logger?.LogDebug("Transaction {TransactionId} rolled back successfully", Id);

            // Notify that transaction is completed
            _onTransactionCompleted?.Invoke(Id);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _state = TransactionState.Aborted;
            _logger?.LogError(ex, "Failed to rollback transaction {TransactionId}", Id);
            
            // Notify that transaction is completed (aborted)
            _onTransactionCompleted?.Invoke(Id);
            
            throw;
        }
    }

    /// <summary>
    /// Creates a savepoint within the transaction.
    /// </summary>
    /// <param name="name">Savepoint name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Savepoint object</returns>
    public async Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        if (_state != TransactionState.Active)
        {
            throw new InvalidOperationException($"Cannot create savepoint in transaction state {_state}");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Savepoint name cannot be null or empty", nameof(name));
        }

        if (_savepoints.Any(s => s.Name == name))
        {
            throw new InvalidOperationException($"Savepoint '{name}' already exists");
        }

        var savepoint = new Savepoint(name, this);
        _savepoints.Add(savepoint);

        _logger?.LogDebug("Created savepoint '{SavepointName}' in transaction {TransactionId}", name, Id);

        return await Task.FromResult(savepoint);
    }

    /// <summary>
    /// Rolls back to a specific savepoint.
    /// </summary>
    /// <param name="savepoint">Savepoint to rollback to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task RollbackToSavepointAsync(ISavepoint savepoint, CancellationToken cancellationToken = default)
    {
        if (_state != TransactionState.Active)
        {
            throw new InvalidOperationException($"Cannot rollback to savepoint in transaction state {_state}");
        }

        if (savepoint.Transaction != this)
        {
            throw new ArgumentException("Savepoint does not belong to this transaction", nameof(savepoint));
        }

        if (!_savepoints.Contains(savepoint))
        {
            throw new ArgumentException("Savepoint not found in this transaction", nameof(savepoint));
        }

        try
        {
            _logger?.LogDebug("Rolling back to savepoint '{SavepointName}' in transaction {TransactionId}", 
                savepoint.Name, Id);

            // Remove all savepoints created after this one
            var savepointIndex = _savepoints.IndexOf(savepoint);
            for (int i = _savepoints.Count - 1; i > savepointIndex; i--)
            {
                _savepoints.RemoveAt(i);
            }

            // In a real implementation, this would rollback to the savepoint state
            // For now, we'll just simulate the rollback

            _logger?.LogDebug("Rolled back to savepoint '{SavepointName}' in transaction {TransactionId}", 
                savepoint.Name, Id);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to rollback to savepoint '{SavepointName}' in transaction {TransactionId}", 
                savepoint.Name, Id);
            throw;
        }
    }

    /// <summary>
    /// Disposes the transaction, rolling back if still active.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_state == TransactionState.Active)
        {
            await RollbackAsync();
        }

        _savepoints.Clear();
        await Task.CompletedTask;
    }
}
