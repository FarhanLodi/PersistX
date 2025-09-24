using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PersistX.Enums;
using PersistX.Interfaces;
using PersistX.Models;

namespace PersistX.Backup;

/// <summary>
/// File-based backup provider for automated backup and point-in-time recovery.
/// </summary>
public class FileBackupProvider : IBackupProvider
{
    private readonly ILogger<FileBackupProvider>? _logger;
    private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
    private readonly ConcurrentDictionary<string, BackupMetadata> _backupCache = new();
    
    private string _backupBasePath = string.Empty;
    private string _backupIndexFilePath = string.Empty;
    private bool _disposed;

    public string Name => "FileBackupProvider";

    public FileBackupProvider(ILogger<FileBackupProvider>? logger = null)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(IBackupProviderConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _backupBasePath = configuration.GetValue("BackupPath", Path.Combine(Environment.CurrentDirectory, "persistx_backups"));

        if (!Directory.Exists(_backupBasePath))
        {
            Directory.CreateDirectory(_backupBasePath);
        }

        _backupIndexFilePath = Path.Combine(_backupBasePath, "backup_index.json");

        // Load existing backup index
        await LoadBackupIndexAsync(cancellationToken);

        _logger?.LogInformation("FileBackupProvider initialized with backup path: {BackupPath}", _backupBasePath);
    }

    public async Task<BackupMetadata> CreateBackupAsync(IBackend backend, string backupId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _operationSemaphore.WaitAsync(cancellationToken);
        try
        {
            var startTime = DateTime.UtcNow;
            var backupPath = GetBackupPath(backupId);
            
            if (Directory.Exists(backupPath))
            {
                throw new InvalidOperationException($"Backup {backupId} already exists");
            }

            Directory.CreateDirectory(backupPath);

            var metadata = new BackupMetadata
            {
                BackupId = backupId,
                Type = BackupType.Full,
                CreatedAt = startTime,
                Status = BackupStatus.InProgress,
                Description = "Full backup"
            };

            _backupCache[backupId] = metadata;

            try
            {
                var locationCount = 0;
                var totalSize = 0L;

                // Create backup archive
                var backupFilePath = Path.Combine(backupPath, "backup.zip");
                using (var fileStream = new FileStream(backupFilePath, FileMode.Create))
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
                {
                    // Backup all locations
                    await foreach (var location in backend.ListLocationsAsync(cancellationToken: cancellationToken))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        var size = await backend.GetSizeAsync(location, cancellationToken);
                        if (size > 0)
                        {
                            var data = await backend.ReadAsync(location, 0, (int)size, cancellationToken);
                            
                            var entry = archive.CreateEntry(location);
                            using var entryStream = entry.Open();
                            await entryStream.WriteAsync(data, cancellationToken);
                            
                            locationCount++;
                            totalSize += size;
                        }
                    }
                } // Archive and file stream are explicitly disposed here

                // Calculate checksum after archive is fully disposed
                var checksum = await CalculateChecksumAsync(backupFilePath, cancellationToken);

                // Update metadata
                metadata = metadata with
                {
                    Status = BackupStatus.Completed,
                    SizeBytes = totalSize,
                    LocationCount = locationCount,
                    Checksum = checksum
                };

                _backupCache[backupId] = metadata;
                await SaveBackupIndexAsync(cancellationToken);

                _logger?.LogInformation("Created full backup {BackupId} with {LocationCount} locations, {SizeBytes} bytes", 
                    backupId, locationCount, totalSize);

                return metadata;
            }
            catch (Exception ex)
            {
                // Update metadata to failed status
                metadata = metadata with { Status = BackupStatus.Failed };
                _backupCache[backupId] = metadata;
                await SaveBackupIndexAsync(cancellationToken);

                _logger?.LogError(ex, "Failed to create backup {BackupId}", backupId);
                throw;
            }
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    public async Task<BackupMetadata> CreateIncrementalBackupAsync(IBackend backend, string backupId, string baseBackupId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _operationSemaphore.WaitAsync(cancellationToken);
        try
        {
            var baseBackup = await GetBackupMetadataAsync(baseBackupId, cancellationToken);
            if (baseBackup == null)
            {
                throw new InvalidOperationException($"Base backup {baseBackupId} not found");
            }

            var startTime = DateTime.UtcNow;
            var backupPath = GetBackupPath(backupId);
            
            if (Directory.Exists(backupPath))
            {
                throw new InvalidOperationException($"Backup {backupId} already exists");
            }

            Directory.CreateDirectory(backupPath);

            var metadata = new BackupMetadata
            {
                BackupId = backupId,
                Type = BackupType.Incremental,
                BaseBackupId = baseBackupId,
                CreatedAt = startTime,
                Status = BackupStatus.InProgress,
                Description = $"Incremental backup based on {baseBackupId}"
            };

            _backupCache[backupId] = metadata;

            try
            {
                var locationCount = 0;
                var totalSize = 0L;

                // Create incremental backup archive
                var backupFilePath = Path.Combine(backupPath, "incremental.zip");
                using (var fileStream = new FileStream(backupFilePath, FileMode.Create))
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
                {
                    // For incremental backup, we'd typically compare with the base backup
                    // This is a simplified implementation that backs up all current data
                    await foreach (var location in backend.ListLocationsAsync(cancellationToken: cancellationToken))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        var size = await backend.GetSizeAsync(location, cancellationToken);
                        if (size > 0)
                        {
                            var data = await backend.ReadAsync(location, 0, (int)size, cancellationToken);
                            
                            var entry = archive.CreateEntry(location);
                            using var entryStream = entry.Open();
                            await entryStream.WriteAsync(data, cancellationToken);
                            
                            locationCount++;
                            totalSize += size;
                        }
                    }
                } // Archive and file stream are explicitly disposed here

                // Calculate checksum after archive is fully disposed
                var checksum = await CalculateChecksumAsync(backupFilePath, cancellationToken);

                // Update metadata
                metadata = metadata with
                {
                    Status = BackupStatus.Completed,
                    SizeBytes = totalSize,
                    LocationCount = locationCount,
                    Checksum = checksum
                };

                _backupCache[backupId] = metadata;
                await SaveBackupIndexAsync(cancellationToken);

                _logger?.LogInformation("Created incremental backup {BackupId} with {LocationCount} locations, {SizeBytes} bytes", 
                    backupId, locationCount, totalSize);

                return metadata;
            }
            catch (Exception ex)
            {
                // Update metadata to failed status
                metadata = metadata with { Status = BackupStatus.Failed };
                _backupCache[backupId] = metadata;
                await SaveBackupIndexAsync(cancellationToken);

                _logger?.LogError(ex, "Failed to create incremental backup {BackupId}", backupId);
                throw;
            }
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    public async Task RestoreBackupAsync(IBackend backend, string backupId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _operationSemaphore.WaitAsync(cancellationToken);
        try
        {
            var metadata = await GetBackupMetadataAsync(backupId, cancellationToken);
            if (metadata == null)
            {
                throw new InvalidOperationException($"Backup {backupId} not found");
            }

            if (metadata.Status != BackupStatus.Completed)
            {
                throw new InvalidOperationException($"Cannot restore backup {backupId} with status {metadata.Status}");
            }

            var backupPath = GetBackupPath(backupId);
            var backupFilePath = Path.Combine(backupPath, metadata.Type == BackupType.Full ? "backup.zip" : "incremental.zip");

            if (!File.Exists(backupFilePath))
            {
                throw new FileNotFoundException($"Backup file not found: {backupFilePath}");
            }

            // Update metadata to restoring status
            var updatedMetadata = metadata with { Status = BackupStatus.Restoring };
            _backupCache[backupId] = updatedMetadata;
            await SaveBackupIndexAsync(cancellationToken);

            try
            {
                using var fileStream = new FileStream(backupFilePath, FileMode.Open);
                using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    using var entryStream = entry.Open();
                    using var memoryStream = new MemoryStream();
                    await entryStream.CopyToAsync(memoryStream, cancellationToken);
                    
                    var data = memoryStream.ToArray();
                    await backend.WriteAsync(entry.FullName, 0, data, cancellationToken);
                }

                // Restore metadata to completed status
                updatedMetadata = updatedMetadata with { Status = BackupStatus.Completed };
                _backupCache[backupId] = updatedMetadata;
                await SaveBackupIndexAsync(cancellationToken);

                _logger?.LogInformation("Restored backup {BackupId} to backend {BackendName}", backupId, backend.Name);
            }
            catch (Exception ex)
            {
                // Restore metadata to completed status (restore operation failed)
                updatedMetadata = updatedMetadata with { Status = BackupStatus.Completed };
                _backupCache[backupId] = updatedMetadata;
                await SaveBackupIndexAsync(cancellationToken);

                _logger?.LogError(ex, "Failed to restore backup {BackupId}", backupId);
                throw;
            }
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    public async IAsyncEnumerable<BackupMetadata> ListBackupsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        foreach (var backup in _backupCache.Values.OrderByDescending(b => b.CreatedAt))
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            yield return backup;
            await Task.Yield();
        }
    }

    public async Task<BackupMetadata?> GetBackupMetadataAsync(string backupId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        return await Task.FromResult(_backupCache.TryGetValue(backupId, out var metadata) ? metadata : null);
    }

    public async Task DeleteBackupAsync(string backupId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _operationSemaphore.WaitAsync(cancellationToken);
        try
        {
            var backupPath = GetBackupPath(backupId);
            if (Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, true);
            }

            _backupCache.TryRemove(backupId, out _);
            await SaveBackupIndexAsync(cancellationToken);

            _logger?.LogInformation("Deleted backup {BackupId}", backupId);
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    public async Task<bool> ValidateBackupAsync(string backupId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var metadata = await GetBackupMetadataAsync(backupId, cancellationToken);
        if (metadata == null)
        {
            return false;
        }

        var backupPath = GetBackupPath(backupId);
        var backupFilePath = Path.Combine(backupPath, metadata.Type == BackupType.Full ? "backup.zip" : "incremental.zip");

        if (!File.Exists(backupFilePath))
        {
            return false;
        }

        try
        {
            var currentChecksum = await CalculateChecksumAsync(backupFilePath, cancellationToken);
            return string.Equals(currentChecksum, metadata.Checksum, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to validate backup {BackupId}", backupId);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        await _operationSemaphore.WaitAsync();
        try
        {
            await SaveBackupIndexAsync(CancellationToken.None);
        }
        finally
        {
            _operationSemaphore.Release();
            _operationSemaphore.Dispose();
        }
    }

    private async Task LoadBackupIndexAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_backupIndexFilePath))
        {
            return;
        }

        try
        {
            var indexData = await File.ReadAllTextAsync(_backupIndexFilePath, cancellationToken);
            var backups = JsonSerializer.Deserialize<List<BackupMetadata>>(indexData);
            
            if (backups != null)
            {
                foreach (var backup in backups)
                {
                    _backupCache[backup.BackupId] = backup;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load backup index");
        }
    }

    private async Task SaveBackupIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            var backups = _backupCache.Values.ToList();
            var indexData = JsonSerializer.Serialize(backups, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_backupIndexFilePath, indexData, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save backup index");
        }
    }

    private string GetBackupPath(string backupId)
    {
        return Path.Combine(_backupBasePath, backupId);
    }

    private async Task<string> CalculateChecksumAsync(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var hash = await sha256.ComputeHashAsync(fileStream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FileBackupProvider));
        }
    }
}
