using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;
using PersistX.Models;
using PersistX.Enums;

namespace PersistX.Storage;

/// <summary>
/// Write-Ahead Log implementation for crash recovery and durability.
/// </summary>
public class WriteAheadLog : IWriteAheadLog
{
    private readonly ILogger<WriteAheadLog>? _logger;
    private readonly ConcurrentDictionary<long, WalEntry> _pendingEntries = new();
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly SemaphoreSlim _replaySemaphore = new(1, 1);
    
    private string _walFilePath = string.Empty;
    private string _walIndexFilePath = string.Empty;
    private FileStream? _walFileStream;
    private BinaryWriter? _walWriter;
    private BinaryReader? _walReader;
    private long _nextLogEntryId = 1;
    private long _lastCommittedEntryId = 0;
    private bool _disposed;

    public string Name => "WriteAheadLog";

    public WriteAheadLog(ILogger<WriteAheadLog>? logger = null)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(IWriteAheadLogConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var basePath = configuration.GetValue("BasePath", Path.Combine(Environment.CurrentDirectory, "persistx_wal"));
        var maxSizeBytes = long.Parse(configuration.GetValue("MaxSizeBytes", "104857600")); // 100MB default

        if (!Directory.Exists(basePath))
        {
            Directory.CreateDirectory(basePath);
        }

        _walFilePath = Path.Combine(basePath, "wal.log");
        _walIndexFilePath = Path.Combine(basePath, "wal.index");

        // Open or create WAL file
        _walFileStream = new FileStream(_walFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.SequentialScan);
        _walWriter = new BinaryWriter(_walFileStream, System.Text.Encoding.UTF8, leaveOpen: true);
        _walReader = new BinaryReader(_walFileStream, System.Text.Encoding.UTF8, leaveOpen: true);

        // Load existing WAL index
        await LoadWalIndexAsync(cancellationToken);

        _logger?.LogInformation("WriteAheadLog initialized with base path: {BasePath}, max size: {MaxSizeBytes} bytes", basePath, maxSizeBytes);
    }

    public async Task<long> LogWriteAsync(string location, long offset, ReadOnlyMemory<byte> data, long transactionId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            var entryId = Interlocked.Increment(ref _nextLogEntryId);
            var entry = new WalEntry
            {
                Id = entryId,
                Type = WalEntryType.Write,
                Location = location,
                Offset = offset,
                Data = data.ToArray(),
                TransactionId = transactionId,
                Timestamp = DateTime.UtcNow
            };

            await WriteEntryAsync(entry, cancellationToken);
            _pendingEntries[entryId] = entry;

            _logger?.LogDebug("Logged write entry {EntryId} for location {Location} at offset {Offset}", entryId, location, offset);
            return entryId;
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    public async Task<long> LogDeleteAsync(string location, long transactionId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            var entryId = Interlocked.Increment(ref _nextLogEntryId);
            var entry = new WalEntry
            {
                Id = entryId,
                Type = WalEntryType.Delete,
                Location = location,
                TransactionId = transactionId,
                Timestamp = DateTime.UtcNow
            };

            await WriteEntryAsync(entry, cancellationToken);
            _pendingEntries[entryId] = entry;

            _logger?.LogDebug("Logged delete entry {EntryId} for location {Location}", entryId, location);
            return entryId;
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    public async Task LogCommitAsync(long transactionId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            var entryId = Interlocked.Increment(ref _nextLogEntryId);
            var entry = new WalEntry
            {
                Id = entryId,
                Type = WalEntryType.Commit,
                TransactionId = transactionId,
                Timestamp = DateTime.UtcNow
            };

            await WriteEntryAsync(entry, cancellationToken);
            _pendingEntries[entryId] = entry;

            // Update last committed entry ID
            _lastCommittedEntryId = entryId;

            // Remove committed entries from pending
            var entriesToRemove = _pendingEntries.Where(kvp => kvp.Value.TransactionId == transactionId).ToList();
            foreach (var kvp in entriesToRemove)
            {
                _pendingEntries.TryRemove(kvp.Key, out _);
            }

            _logger?.LogDebug("Logged commit entry {EntryId} for transaction {TransactionId}", entryId, transactionId);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    public async Task LogRollbackAsync(long transactionId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            var entryId = Interlocked.Increment(ref _nextLogEntryId);
            var entry = new WalEntry
            {
                Id = entryId,
                Type = WalEntryType.Rollback,
                TransactionId = transactionId,
                Timestamp = DateTime.UtcNow
            };

            await WriteEntryAsync(entry, cancellationToken);
            _pendingEntries[entryId] = entry;

            // Remove rolled back entries from pending
            var entriesToRemove = _pendingEntries.Where(kvp => kvp.Value.TransactionId == transactionId).ToList();
            foreach (var kvp in entriesToRemove)
            {
                _pendingEntries.TryRemove(kvp.Key, out _);
            }

            _logger?.LogDebug("Logged rollback entry {EntryId} for transaction {TransactionId}", entryId, transactionId);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    public async Task ReplayAsync(IBackend backend, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _replaySemaphore.WaitAsync(cancellationToken);
        try
        {
            _logger?.LogInformation("Starting WAL replay to backend {BackendName}", backend.Name);

            var replayedCount = 0;
            var committedTransactions = new HashSet<long>();

            // Read all entries from WAL file
            _walFileStream!.Seek(0, SeekOrigin.Begin);
            
            while (_walFileStream.Position < _walFileStream.Length)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var entry = await ReadEntryAsync(cancellationToken);
                if (entry == null)
                    break;

                // Track committed transactions
                if (entry.Type == WalEntryType.Commit)
                {
                    committedTransactions.Add(entry.TransactionId);
                }
                else if (entry.Type == WalEntryType.Rollback)
                {
                    committedTransactions.Remove(entry.TransactionId);
                }
            }

            // Replay only committed operations
            _walFileStream.Seek(0, SeekOrigin.Begin);
            
            while (_walFileStream.Position < _walFileStream.Length)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var entry = await ReadEntryAsync(cancellationToken);
                if (entry == null)
                    break;

                // Only replay operations from committed transactions
                if (committedTransactions.Contains(entry.TransactionId))
                {
                    switch (entry.Type)
                    {
                        case WalEntryType.Write:
                            await backend.WriteAsync(entry.Location!, entry.Offset, entry.Data, cancellationToken);
                            replayedCount++;
                            break;
                        case WalEntryType.Delete:
                            await backend.DeleteAsync(entry.Location!, cancellationToken);
                            replayedCount++;
                            break;
                    }
                }
            }

            _logger?.LogInformation("WAL replay completed. Replayed {ReplayedCount} operations", replayedCount);
        }
        finally
        {
            _replaySemaphore.Release();
        }
    }

    public async Task TruncateAsync(long upToLogEntry, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            // This is a simplified implementation
            // In a production system, you'd want to implement proper WAL truncation
            // that maintains the integrity of the log file
            
            _logger?.LogInformation("WAL truncation requested up to entry {UpToLogEntry}", upToLogEntry);
            await Task.CompletedTask;
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    public async Task<long> GetSizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (_walFileStream == null)
            return 0;

        return await Task.FromResult(_walFileStream.Length);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (_walFileStream != null)
        {
            await _walFileStream.FlushAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        await _writeSemaphore.WaitAsync();
        await _replaySemaphore.WaitAsync();

        try
        {
            _walWriter?.Dispose();
            _walReader?.Dispose();
            _walFileStream?.Dispose();
            
            await SaveWalIndexAsync(CancellationToken.None);
        }
        finally
        {
            _writeSemaphore.Release();
            _replaySemaphore.Release();
            _writeSemaphore.Dispose();
            _replaySemaphore.Dispose();
        }
    }

    private async Task WriteEntryAsync(WalEntry entry, CancellationToken cancellationToken)
    {
        if (_walWriter == null)
            throw new InvalidOperationException("WAL not initialized");

        // Write entry header
        _walWriter.Write(entry.Id);
        _walWriter.Write((int)entry.Type);
        _walWriter.Write(entry.TransactionId);
        _walWriter.Write(entry.Timestamp.ToBinary());
        _walWriter.Write(entry.Location ?? string.Empty);
        _walWriter.Write(entry.Offset);

        // Write data length and data
        _walWriter.Write(entry.Data.Length);
        if (entry.Data.Length > 0)
        {
            _walWriter.Write(entry.Data);
        }

        await _walFileStream!.FlushAsync(cancellationToken);
    }

    private Task<WalEntry?> ReadEntryAsync(CancellationToken cancellationToken)
    {
        if (_walReader == null || _walFileStream == null)
            return Task.FromResult<WalEntry?>(null);

        try
        {
            var id = _walReader.ReadInt64();
            var type = (WalEntryType)_walReader.ReadInt32();
            var transactionId = _walReader.ReadInt64();
            var timestamp = DateTime.FromBinary(_walReader.ReadInt64());
            var location = _walReader.ReadString();
            var offset = _walReader.ReadInt64();
            var dataLength = _walReader.ReadInt32();
            var data = dataLength > 0 ? _walReader.ReadBytes(dataLength) : Array.Empty<byte>();

            return Task.FromResult<WalEntry?>(new WalEntry
            {
                Id = id,
                Type = type,
                TransactionId = transactionId,
                Timestamp = timestamp,
                Location = string.IsNullOrEmpty(location) ? null : location,
                Offset = offset,
                Data = data
            });
        }
        catch (EndOfStreamException)
        {
            return Task.FromResult<WalEntry?>(null);
        }
    }

    private async Task LoadWalIndexAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_walIndexFilePath))
        {
            _nextLogEntryId = 1;
            _lastCommittedEntryId = 0;
            return;
        }

        try
        {
            var indexData = await File.ReadAllTextAsync(_walIndexFilePath, cancellationToken);
            var index = JsonSerializer.Deserialize<WalIndex>(indexData);
            
            if (index != null)
            {
                _nextLogEntryId = index.NextLogEntryId;
                _lastCommittedEntryId = index.LastCommittedEntryId;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load WAL index, using defaults");
            _nextLogEntryId = 1;
            _lastCommittedEntryId = 0;
        }
    }

    private async Task SaveWalIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            var index = new WalIndex
            {
                NextLogEntryId = _nextLogEntryId,
                LastCommittedEntryId = _lastCommittedEntryId
            };

            var indexData = JsonSerializer.Serialize(index);
            await File.WriteAllTextAsync(_walIndexFilePath, indexData, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save WAL index");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WriteAheadLog));
        }
    }

}
