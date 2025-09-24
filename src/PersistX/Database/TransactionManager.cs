using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;
using PersistX.Enums;

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
            var transaction = new Transaction(Guid.NewGuid(), isolationLevel, _logger as ILogger<Transaction>, RemoveCompletedTransaction);
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

    /// <summary>
    /// Removes a completed transaction from the active transactions list.
    /// </summary>
    /// <param name="transactionId">The ID of the transaction to remove</param>
    public void RemoveCompletedTransaction(Guid transactionId)
    {
        ThrowIfDisposed();
        
        if (_activeTransactions.TryRemove(transactionId, out var transaction))
        {
            if (transaction == _currentTransaction.Value)
            {
                _currentTransaction.Value = null;
            }
            _logger?.LogDebug("Removed completed transaction {TransactionId} from active transactions", transactionId);
        }
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

