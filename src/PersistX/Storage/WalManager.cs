using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PersistX.Storage;

public sealed class WalManager : IAsyncDisposable
{
    private readonly string _walFilePath;
    private readonly ILogger<WalManager>? _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public WalManager(string walFilePath, ILogger<WalManager>? logger = null)
    {
        _walFilePath = walFilePath ?? throw new ArgumentNullException(nameof(walFilePath));
        _logger = logger;

        // Ensure the directory exists
        var dir = Path.GetDirectoryName(_walFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Append a single WAL entry (serialized as a JSON line) to the WAL file.
    /// </summary>
    public async Task AppendAsync(WalEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var line = JsonSerializer.Serialize(entry, JsonOptions);
            await using var fs = new FileStream(
                _walFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);
            await using var writer = new StreamWriter(fs);
            await writer.WriteLineAsync(line).ConfigureAwait(false);
            await writer.FlushAsync(ct).ConfigureAwait(false);

            _logger?.LogDebug(
                "WAL appended: TxId={TransactionId} Op={OperationType} Location={Location}",
                entry.TransactionId, entry.OperationType, entry.Location);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Read all entries for a specific transaction.
    /// </summary>
    public async Task<IReadOnlyList<WalEntry>> ReadTransactionEntriesAsync(
        Guid transactionId, CancellationToken ct = default)
    {
        var all = await ReadAllEntriesAsync(ct).ConfigureAwait(false);
        return all.Where(e => e.TransactionId == transactionId).ToList();
    }

    /// <summary>
    /// Read ALL entries from the WAL file.
    /// </summary>
    public async Task<IReadOnlyList<WalEntry>> ReadAllEntriesAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await ReadAllEntriesInternalAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Remove entries for committed/rolled-back transactions (compaction).
    /// Rewrites the WAL file containing only entries for still-active transactions.
    /// </summary>
    public async Task TruncateCompletedAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entries = await ReadAllEntriesInternalAsync(ct).ConfigureAwait(false);

            // Find all transaction IDs that have a Commit or Rollback entry
            var completedTxIds = entries
                .Where(e => e.OperationType is WalOperationType.Commit or WalOperationType.Rollback)
                .Select(e => e.TransactionId)
                .ToHashSet();

            // Keep only entries that belong to still-active transactions
            var remaining = entries
                .Where(e => !completedTxIds.Contains(e.TransactionId))
                .ToList();

            // Rewrite the file atomically via a temp file
            var tempPath = _walFilePath + ".tmp";
            await using (var fs = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            await using (var writer = new StreamWriter(fs))
            {
                foreach (var entry in remaining)
                {
                    var line = JsonSerializer.Serialize(entry, JsonOptions);
                    await writer.WriteLineAsync(line).ConfigureAwait(false);
                }
                await writer.FlushAsync(ct).ConfigureAwait(false);
            }

            File.Move(tempPath, _walFilePath, overwrite: true);

            _logger?.LogDebug(
                "WAL truncated: removed {RemovedCount} completed transactions, {RemainingCount} entries remain.",
                completedTxIds.Count, remaining.Count);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Returns true if any transactions have Write entries but no Commit or Rollback.
    /// </summary>
    public async Task<bool> HasUncommittedTransactionsAsync(CancellationToken ct = default)
    {
        var ids = await GetUncommittedTransactionIdsAsync(ct).ConfigureAwait(false);
        return ids.Count > 0;
    }

    /// <summary>
    /// Get IDs of uncommitted transactions (have Write/Delete entries but no Commit or Rollback).
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetUncommittedTransactionIdsAsync(CancellationToken ct = default)
    {
        var all = await ReadAllEntriesAsync(ct).ConfigureAwait(false);

        var writeTxIds = all
            .Where(e => e.OperationType is WalOperationType.Write or WalOperationType.Delete)
            .Select(e => e.TransactionId)
            .ToHashSet();

        var finishedTxIds = all
            .Where(e => e.OperationType is WalOperationType.Commit or WalOperationType.Rollback)
            .Select(e => e.TransactionId)
            .ToHashSet();

        var uncommitted = writeTxIds
            .Where(id => !finishedTxIds.Contains(id))
            .ToList();

        return uncommitted;
    }

    public async ValueTask DisposeAsync()
    {
        _semaphore.Dispose();
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    // Internal helper — caller MUST hold _semaphore
    private async Task<IReadOnlyList<WalEntry>> ReadAllEntriesInternalAsync(CancellationToken ct)
    {
        if (!File.Exists(_walFilePath))
            return Array.Empty<WalEntry>();

        var entries = new List<WalEntry>();

        await using var fs = new FileStream(
            _walFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        using var reader = new StreamReader(fs);

        string? line;
        int lineNumber = 0;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var entry = JsonSerializer.Deserialize<WalEntry>(line, JsonOptions);
                if (entry is not null)
                    entries.Add(entry);
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning(ex,
                    "WAL: skipping malformed entry at line {LineNumber}.", lineNumber);
            }
        }

        return entries;
    }
}
