using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;

namespace PersistX.Storage;

/// <summary>
/// A decorator that wraps any <see cref="IBackend"/> and adds WAL journaling
/// for transactional write and delete operations, enabling ACID transactions.
/// </summary>
public sealed class WalBackend : IBackend
{
    private readonly IBackend _inner;
    private readonly WalManager _walManager;
    private readonly ILogger<WalBackend>? _logger;

    // Outer key: transaction ID
    // Inner key: location
    // Value: (isDelete, offset, data) — data is empty for deletes
    private readonly Dictionary<Guid, Dictionary<string, (bool isDelete, long offset, byte[] data)>> _buffers = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public WalBackend(IBackend inner, WalManager walManager, ILogger<WalBackend>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _walManager = walManager ?? throw new ArgumentNullException(nameof(walManager));
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // IBackend: identity / delegation
    // -------------------------------------------------------------------------

    public string Name => $"WAL({_inner.Name})";

    public Task InitializeAsync(IBackendConfiguration configuration, CancellationToken cancellationToken = default)
        => _inner.InitializeAsync(configuration, cancellationToken);

    public Task<bool> ExistsAsync(string location, CancellationToken cancellationToken = default)
        => _inner.ExistsAsync(location, cancellationToken);

    public Task<long> GetSizeAsync(string location, CancellationToken cancellationToken = default)
        => _inner.GetSizeAsync(location, cancellationToken);

    public IAsyncEnumerable<string> ListLocationsAsync(string? pattern = null, CancellationToken cancellationToken = default)
        => _inner.ListLocationsAsync(pattern, cancellationToken);

    public Task FlushAsync(CancellationToken cancellationToken = default)
        => _inner.FlushAsync(cancellationToken);

    // -------------------------------------------------------------------------
    // IBackend: transactionally-aware read
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reads data. If a transaction is active and contains a buffered write for
    /// this location, returns the buffered data (read-your-writes semantics).
    /// </summary>
    public async Task<ReadOnlyMemory<byte>> ReadAsync(
        string location, long offset, int length, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Check all active transaction buffers for a pending write to this location
            foreach (var (_, buffer) in _buffers)
            {
                if (buffer.TryGetValue(location, out var pending) && !pending.isDelete)
                {
                    // Return the buffered slice (read-your-writes)
                    var buf = pending.data;
                    var start = (int)Math.Min(offset - pending.offset, buf.Length);
                    start = Math.Max(start, 0);
                    var available = Math.Max(buf.Length - start, 0);
                    var count = Math.Min(length, available);
                    return new ReadOnlyMemory<byte>(buf, start, count);
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        return await _inner.ReadAsync(location, offset, length, cancellationToken).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // IBackend: transactionally-aware write
    // -------------------------------------------------------------------------

    /// <summary>
    /// Writes data. If a transaction is active, the write is buffered in memory
    /// AND journaled to the WAL. Otherwise it is written directly to the inner backend.
    /// </summary>
    public async Task WriteAsync(
        string location, long offset, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        Guid? activeTxId = null;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Pick the first active transaction (single active transaction per thread is the expected usage)
            foreach (var txId in _buffers.Keys)
            {
                activeTxId = txId;
                break;
            }

            if (activeTxId.HasValue)
            {
                // Buffer the write
                _buffers[activeTxId.Value][location] = (false, offset, data.ToArray());
            }
        }
        finally
        {
            _lock.Release();
        }

        if (activeTxId.HasValue)
        {
            // Journal to WAL outside the lock (I/O should not be under lock)
            var entry = new WalEntry
            {
                TransactionId = activeTxId.Value,
                OperationType = WalOperationType.Write,
                Location = location,
                Offset = offset,
                Data = data.ToArray()
            };
            await _walManager.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
            _logger?.LogDebug("WAL Write buffered: TxId={TxId} Location={Location}", activeTxId.Value, location);
        }
        else
        {
            // No active transaction — write directly
            await _inner.WriteAsync(location, offset, data, cancellationToken).ConfigureAwait(false);
        }
    }

    // -------------------------------------------------------------------------
    // IBackend: transactionally-aware delete
    // -------------------------------------------------------------------------

    /// <summary>
    /// Deletes a location. If a transaction is active, the delete is buffered AND
    /// journaled to the WAL. Otherwise it is executed directly on the inner backend.
    /// </summary>
    public async Task DeleteAsync(string location, CancellationToken cancellationToken = default)
    {
        Guid? activeTxId = null;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var txId in _buffers.Keys)
            {
                activeTxId = txId;
                break;
            }

            if (activeTxId.HasValue)
            {
                // Mark as a pending delete (offset and data are irrelevant)
                _buffers[activeTxId.Value][location] = (true, 0L, Array.Empty<byte>());
            }
        }
        finally
        {
            _lock.Release();
        }

        if (activeTxId.HasValue)
        {
            var entry = new WalEntry
            {
                TransactionId = activeTxId.Value,
                OperationType = WalOperationType.Delete,
                Location = location,
                Offset = 0,
                Data = null
            };
            await _walManager.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
            _logger?.LogDebug("WAL Delete buffered: TxId={TxId} Location={Location}", activeTxId.Value, location);
        }
        else
        {
            await _inner.DeleteAsync(location, cancellationToken).ConfigureAwait(false);
        }
    }

    // -------------------------------------------------------------------------
    // Transaction management
    // -------------------------------------------------------------------------

    /// <summary>
    /// Begins a new transaction. All subsequent writes and deletes on this
    /// <see cref="WalBackend"/> instance are journaled until committed or rolled back.
    /// </summary>
    public void BeginTransaction(Guid transactionId)
    {
        _lock.Wait();
        try
        {
            if (_buffers.ContainsKey(transactionId))
                throw new InvalidOperationException(
                    $"Transaction {transactionId} is already active.");

            _buffers[transactionId] = new Dictionary<string, (bool isDelete, long offset, byte[] data)>();
            _logger?.LogDebug("Transaction started: TxId={TxId}", transactionId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Commits a transaction: flushes all buffered writes/deletes to the inner
    /// backend and appends a Commit entry to the WAL.
    /// </summary>
    public async Task CommitTransactionAsync(Guid transactionId, CancellationToken ct = default)
    {
        Dictionary<string, (bool isDelete, long offset, byte[] data)>? buffer;

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_buffers.TryGetValue(transactionId, out buffer))
                throw new InvalidOperationException(
                    $"Transaction {transactionId} is not active.");

            // Remove buffer immediately so no new operations are added while we flush
            _buffers.Remove(transactionId);
        }
        finally
        {
            _lock.Release();
        }

        // Apply buffered operations to the inner backend
        foreach (var (location, pending) in buffer)
        {
            if (pending.isDelete)
                await _inner.DeleteAsync(location, ct).ConfigureAwait(false);
            else
                await _inner.WriteAsync(location, pending.offset, pending.data, ct).ConfigureAwait(false);
        }

        await _inner.FlushAsync(ct).ConfigureAwait(false);

        // Journal the commit
        var commitEntry = new WalEntry
        {
            TransactionId = transactionId,
            OperationType = WalOperationType.Commit,
            Location = string.Empty,
            Offset = 0,
            Data = null
        };
        await _walManager.AppendAsync(commitEntry, ct).ConfigureAwait(false);

        _logger?.LogInformation("Transaction committed: TxId={TxId}, Operations={Count}",
            transactionId, buffer.Count);
    }

    /// <summary>
    /// Rolls back a transaction: discards all buffered writes/deletes and appends
    /// a Rollback entry to the WAL.
    /// </summary>
    public async Task RollbackTransactionAsync(Guid transactionId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_buffers.ContainsKey(transactionId))
                throw new InvalidOperationException(
                    $"Transaction {transactionId} is not active.");

            _buffers.Remove(transactionId);
        }
        finally
        {
            _lock.Release();
        }

        // Journal the rollback
        var rollbackEntry = new WalEntry
        {
            TransactionId = transactionId,
            OperationType = WalOperationType.Rollback,
            Location = string.Empty,
            Offset = 0,
            Data = null
        };
        await _walManager.AppendAsync(rollbackEntry, ct).ConfigureAwait(false);

        _logger?.LogInformation("Transaction rolled back: TxId={TxId}", transactionId);
    }

    // -------------------------------------------------------------------------
    // Crash recovery
    // -------------------------------------------------------------------------

    /// <summary>
    /// Replays uncommitted transactions from the WAL on startup.
    /// Any transaction that has Write/Delete entries but no Commit or Rollback
    /// is re-applied to the inner backend and then marked as committed.
    /// Call this once during application startup before accepting new operations.
    /// </summary>
    public async Task RecoverAsync(CancellationToken ct = default)
    {
        var uncommittedIds = await _walManager.GetUncommittedTransactionIdsAsync(ct).ConfigureAwait(false);

        if (uncommittedIds.Count == 0)
        {
            _logger?.LogInformation("WAL recovery: no uncommitted transactions found.");
            return;
        }

        _logger?.LogWarning(
            "WAL recovery: found {Count} uncommitted transaction(s). Replaying...",
            uncommittedIds.Count);

        foreach (var txId in uncommittedIds)
        {
            var entries = await _walManager.ReadTransactionEntriesAsync(txId, ct).ConfigureAwait(false);

            foreach (var entry in entries)
            {
                switch (entry.OperationType)
                {
                    case WalOperationType.Write when entry.Data is not null:
                        await _inner.WriteAsync(entry.Location, entry.Offset, entry.Data, ct)
                            .ConfigureAwait(false);
                        _logger?.LogDebug(
                            "Recovery: replayed Write TxId={TxId} Location={Location}",
                            txId, entry.Location);
                        break;

                    case WalOperationType.Delete:
                        if (await _inner.ExistsAsync(entry.Location, ct).ConfigureAwait(false))
                            await _inner.DeleteAsync(entry.Location, ct).ConfigureAwait(false);
                        _logger?.LogDebug(
                            "Recovery: replayed Delete TxId={TxId} Location={Location}",
                            txId, entry.Location);
                        break;
                }
            }

            await _inner.FlushAsync(ct).ConfigureAwait(false);

            // Mark the recovered transaction as committed in the WAL
            var commitEntry = new WalEntry
            {
                TransactionId = txId,
                OperationType = WalOperationType.Commit,
                Location = string.Empty,
                Offset = 0,
                Data = null
            };
            await _walManager.AppendAsync(commitEntry, ct).ConfigureAwait(false);

            _logger?.LogInformation("Recovery: committed TxId={TxId}", txId);
        }

        // Compact the WAL now that everything is committed
        await _walManager.TruncateCompletedAsync(ct).ConfigureAwait(false);
        _logger?.LogInformation("WAL recovery complete. WAL compacted.");
    }

    // -------------------------------------------------------------------------
    // IAsyncDisposable
    // -------------------------------------------------------------------------

    public async ValueTask DisposeAsync()
    {
        _lock.Dispose();
        await _inner.DisposeAsync().ConfigureAwait(false);
        await _walManager.DisposeAsync().ConfigureAwait(false);
    }
}
