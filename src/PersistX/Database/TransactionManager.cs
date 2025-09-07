using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;

namespace PersistX.Database;

/// <summary>
/// Manages transactions for a database.
/// </summary>
public class TransactionManager : ITransactionManager, IAsyncDisposable
{
    private readonly ILogger<TransactionManager>? _logger;
    private readonly ConcurrentDictionary<Guid, ITransaction> _activeTransactions = new();
    private readonly AsyncLocal<ITransaction?> _currentTransaction = new();
    private bool _disposed;

    public TransactionManager(ILogger<TransactionManager>? logger = null)
    {
        _logger = logger;
    }

    public async Task<ITransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            var transaction = new Transaction(Guid.NewGuid(), isolationLevel, _logger as ILogger<Transaction>);
            _activeTransactions.TryAdd(transaction.Id, transaction);
            _currentTransaction.Value = transaction;

            _logger?.LogDebug("Started transaction {TransactionId} with isolation level {IsolationLevel}", 
                transaction.Id, isolationLevel);

            return await Task.FromResult(transaction);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to begin transaction with isolation level {IsolationLevel}", isolationLevel);
            throw;
        }
    }

    public ITransaction? GetCurrentTransaction()
    {
        ThrowIfDisposed();
        return _currentTransaction.Value;
    }

    public IReadOnlyList<ITransaction> GetActiveTransactions()
    {
        ThrowIfDisposed();
        return _activeTransactions.Values.ToList();
    }

    public async Task DetectAndResolveDeadlocksAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            // Simple deadlock detection - in a real implementation, this would be more sophisticated
            var transactions = _activeTransactions.Values.ToList();
            
            // Check for transactions that have been active for too long
            var longRunningTransactions = transactions
                .Where(t => t.State == TransactionState.Active)
                .Where(t => t is Transaction tx && DateTime.UtcNow - tx.CreatedAt > TimeSpan.FromMinutes(5))
                .ToList();

            foreach (var transaction in longRunningTransactions)
            {
                _logger?.LogWarning("Rolling back long-running transaction {TransactionId}", transaction.Id);
                await transaction.RollbackAsync(cancellationToken);
            }

            // Remove completed transactions
            var completedTransactions = transactions
                .Where(t => t.State == TransactionState.Committed || 
                           t.State == TransactionState.RolledBack || 
                           t.State == TransactionState.Aborted)
                .ToList();

            foreach (var transaction in completedTransactions)
            {
                _activeTransactions.TryRemove(transaction.Id, out _);
                if (transaction == _currentTransaction.Value)
                {
                    _currentTransaction.Value = null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during deadlock detection and resolution");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // Rollback all active transactions
            var activeTransactions = _activeTransactions.Values.ToList();
            foreach (var transaction in activeTransactions)
            {
                try
                {
                    if (transaction.State == TransactionState.Active)
                    {
                        await transaction.RollbackAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error rolling back transaction {TransactionId} during disposal", transaction.Id);
                }
            }

            _activeTransactions.Clear();
            _currentTransaction.Value = null;

            _logger?.LogDebug("TransactionManager disposed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disposing TransactionManager");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TransactionManager));
        }
    }
}

/// <summary>
/// Basic transaction implementation.
/// </summary>
internal class Transaction : ITransaction
{
    private readonly ILogger<Transaction>? _logger;
    private readonly List<ISavepoint> _savepoints = new();
    private TransactionState _state = TransactionState.Active;

    public Guid Id { get; }
    public IsolationLevel IsolationLevel { get; }
    public TransactionState State => _state;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public Transaction(Guid id, IsolationLevel isolationLevel, ILogger<Transaction>? logger = null)
    {
        Id = id;
        IsolationLevel = isolationLevel;
        _logger = logger;
    }

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

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _state = TransactionState.Aborted;
            _logger?.LogError(ex, "Failed to commit transaction {TransactionId}", Id);
            throw;
        }
    }

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

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _state = TransactionState.Aborted;
            _logger?.LogError(ex, "Failed to rollback transaction {TransactionId}", Id);
            throw;
        }
    }

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

/// <summary>
/// Basic savepoint implementation.
/// </summary>
internal class Savepoint : ISavepoint
{
    public string Name { get; }
    public ITransaction Transaction { get; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public Savepoint(string name, ITransaction transaction)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }
}
