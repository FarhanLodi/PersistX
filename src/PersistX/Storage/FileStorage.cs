using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.IO.MemoryMappedFiles;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;

namespace PersistX.Storage;

/// <summary>
/// File-based storage for PersistX with advanced features: WAL, page storage, compression, encryption, and backup.
/// </summary>
public class FileStorage : IBackend
{
    private readonly ILogger<FileStorage>? _logger;
    private readonly ConcurrentDictionary<string, FileStream> _openFiles = new();
    private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
    
    private string _basePath = string.Empty;
    private IWriteAheadLog? _wal;
    private ICompressionProvider? _compressionProvider;
    private IEncryptionProvider? _encryptionProvider;
    private IBackupProvider? _backupProvider;
    private bool _enableMemoryMappedIO = false;
    private bool _disposed;

    public string Name => "FileStorage";

    public FileStorage(ILogger<FileStorage>? logger = null)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(IBackendConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _basePath = configuration.GetValue("FilePath", Path.Combine(Environment.CurrentDirectory, "persistx_data"));
        
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }

        // Initialize Write-Ahead Log if enabled and not disabled by higher-level component
        var enableWal = bool.Parse(configuration.GetValue("EnableWAL", "false"));
        var disableWal = bool.Parse(configuration.GetValue("DisableWAL", "false"));
        if (enableWal && !disableWal)
        {
            _wal = new WriteAheadLog(_logger as ILogger<WriteAheadLog>);
            var walConfig = new DictionaryConfiguration
            {
                ["BasePath"] = Path.Combine(_basePath, "wal")
            };
            await _wal.InitializeAsync(walConfig, cancellationToken);
        }


        // Initialize Compression Provider if enabled
        var compressionType = configuration.GetValue("CompressionType", "None");
        if (compressionType != "None")
        {
            _compressionProvider = compressionType.ToLowerInvariant() switch
            {
                "gzip" => new Compression.GZipCompressionProvider(_logger as ILogger<Compression.GZipCompressionProvider>),
                "deflate" => new Compression.DeflateCompressionProvider(_logger as ILogger<Compression.DeflateCompressionProvider>),
                _ => throw new NotSupportedException($"Compression type '{compressionType}' is not supported")
            };
        }

        // Initialize Encryption Provider if enabled
        var encryptionType = configuration.GetValue("EncryptionType", "None");
        if (encryptionType != "None")
        {
            _encryptionProvider = encryptionType.ToLowerInvariant() switch
            {
                "aes" => new Encryption.AesEncryptionProvider(_logger as ILogger<Encryption.AesEncryptionProvider>),
                _ => throw new NotSupportedException($"Encryption type '{encryptionType}' is not supported")
            };

            var encryptionConfig = new DictionaryConfiguration();
            var encryptionKey = configuration.GetValue("EncryptionKey");
            if (!string.IsNullOrEmpty(encryptionKey))
            {
                encryptionConfig["Key"] = encryptionKey;
            }
            await _encryptionProvider.InitializeAsync(encryptionConfig, cancellationToken);
        }

        // Initialize Backup Provider if enabled
        var enableBackup = bool.Parse(configuration.GetValue("EnableBackup", "false"));
        if (enableBackup)
        {
            _backupProvider = new Backup.FileBackupProvider(_logger as ILogger<Backup.FileBackupProvider>);
            var backupConfig = new DictionaryConfiguration
            {
                ["BasePath"] = Path.Combine(_basePath, "backups")
            };
            await _backupProvider.InitializeAsync(backupConfig, cancellationToken);
        }

        // Initialize Memory-Mapped I/O if enabled
        _enableMemoryMappedIO = bool.Parse(configuration.GetValue("EnableMemoryMappedIO", "false"));

        _logger?.LogInformation("FileStorage initialized with base path: {BasePath}, WAL: {WALEnabled}, Compression: {CompressionType}, Encryption: {EncryptionType}, Backup: {BackupEnabled}, MMF: {MMFEnabled}", 
            _basePath, enableWal, compressionType, encryptionType, enableBackup, _enableMemoryMappedIO);
    }

    public async Task<ReadOnlyMemory<byte>> ReadAsync(string location, long offset, int length, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _operationSemaphore.WaitAsync(cancellationToken);
        try
        {
            ReadOnlyMemory<byte> data;

            // Use memory-mapped I/O for larger reads if enabled
            if (_enableMemoryMappedIO && length > 1024) // Use MMF for larger reads
            {
                data = await ReadWithMemoryMappedFileAsync(location, offset, length, cancellationToken);
            }
            else
            {
                data = await ReadWithFileStreamAsync(location, offset, length, cancellationToken);
            }

            // Decrypt if encryption is enabled
            if (_encryptionProvider != null && data.Length > 0)
            {
                data = await _encryptionProvider.DecryptAsync(data, cancellationToken);
            }

            // Decompress if compression is enabled
            if (_compressionProvider != null && data.Length > 0)
            {
                data = await _compressionProvider.DecompressAsync(data, cancellationToken);
            }

            return data;
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    public async Task WriteAsync(string location, long offset, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _operationSemaphore.WaitAsync(cancellationToken);
        try
        {
            var processedData = data;

            // Compress if compression is enabled
            if (_compressionProvider != null && data.Length > 0)
            {
                processedData = await _compressionProvider.CompressAsync(processedData, cancellationToken);
            }

            // Encrypt if encryption is enabled
            if (_encryptionProvider != null && processedData.Length > 0)
            {
                processedData = await _encryptionProvider.EncryptAsync(processedData, cancellationToken);
            }

            // Log to WAL if enabled
            if (_wal != null)
            {
                var transactionId = Environment.TickCount64; // Simple transaction ID
                await _wal.LogWriteAsync(location, offset, processedData, transactionId, cancellationToken);
            }

            await WriteRawAsync(location, offset, processedData, cancellationToken);
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    public async Task DeleteAsync(string location, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _operationSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Log to WAL if enabled
            if (_wal != null)
            {
                var transactionId = Environment.TickCount64; // Simple transaction ID
                await _wal.LogDeleteAsync(location, transactionId, cancellationToken);
            }

            await DeleteRawAsync(location, cancellationToken);
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    public async Task<bool> ExistsAsync(string location, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var filePath = GetFilePath(location);
        return await Task.FromResult(File.Exists(filePath));
    }

    public async Task<long> GetSizeAsync(string location, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var filePath = GetFilePath(location);
        
        if (!File.Exists(filePath))
        {
            return -1;
        }

        var fileInfo = new FileInfo(filePath);
        return await Task.FromResult(fileInfo.Length);
    }

    public async IAsyncEnumerable<string> ListLocationsAsync(string? pattern = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var searchPattern = pattern ?? "*";
        var searchPath = Path.Combine(_basePath, "**", searchPattern);
        
        var files = Directory.GetFiles(_basePath, searchPattern, SearchOption.AllDirectories);
        
        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            var relativePath = Path.GetRelativePath(_basePath, file);
            yield return relativePath.Replace('\\', '/');
            
            await Task.Yield(); // Allow cancellation
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        foreach (var fileStream in _openFiles.Values)
        {
            await fileStream.FlushAsync(cancellationToken);
        }

        if (_wal != null)
        {
            await _wal.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Gets the size of the Write-Ahead Log if enabled.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>WAL size in bytes, or 0 if WAL is not enabled</returns>
    public async Task<long> GetWalSizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (_wal != null)
        {
            return await _wal.GetSizeAsync(cancellationToken);
        }
        
        return 0;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        await _operationSemaphore.WaitAsync();
        try
        {
            // Close all open files
            foreach (var fileStream in _openFiles.Values)
            {
                await fileStream.DisposeAsync();
            }
            _openFiles.Clear();

            // Dispose advanced features
            if (_wal is IAsyncDisposable walDisposable)
            {
                await walDisposable.DisposeAsync();
            }


            if (_compressionProvider is IAsyncDisposable compressionDisposable)
            {
                await compressionDisposable.DisposeAsync();
            }

            if (_encryptionProvider is IAsyncDisposable encryptionDisposable)
            {
                await encryptionDisposable.DisposeAsync();
            }

            if (_backupProvider is IAsyncDisposable backupDisposable)
            {
                await backupDisposable.DisposeAsync();
            }
        }
        finally
        {
            _operationSemaphore.Release();
            _operationSemaphore.Dispose();
        }
    }

    // Raw file operations (without advanced features)
    private async Task<ReadOnlyMemory<byte>> ReadRawAsync(string location, long offset, int length, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(location);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        if (_enableMemoryMappedIO && length > 1024) // Use MMF for larger reads
        {
            return await ReadWithMemoryMappedFileAsync(filePath, offset, length, cancellationToken);
        }
        else
        {
            return await ReadWithFileStreamAsync(filePath, offset, length, cancellationToken);
        }
    }

    private async Task<ReadOnlyMemory<byte>> ReadWithMemoryMappedFileAsync(string filePath, long offset, int length, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a new memory-mapped file for each read to avoid handle conflicts
            // This is less efficient but more reliable for concurrent access
            var fileInfo = new FileInfo(filePath);
            using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, fileInfo.Length, MemoryMappedFileAccess.Read);
            using var accessor = mmf.CreateViewAccessor(offset, length, MemoryMappedFileAccess.Read);
            
            var buffer = new byte[length];
            var bytesRead = accessor.ReadArray(0, buffer, 0, length);
            
            _logger?.LogDebug("Read {BytesRead} bytes using memory-mapped I/O from {FilePath} at offset {Offset}", 
                bytesRead, filePath, offset);
            
            return new ReadOnlyMemory<byte>(buffer, 0, bytesRead);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Memory-mapped I/O failed for {FilePath}, falling back to FileStream", filePath);
            return await ReadWithFileStreamAsync(filePath, offset, length, cancellationToken);
        }
    }

    private async Task<ReadOnlyMemory<byte>> ReadWithFileStreamAsync(string filePath, long offset, int length, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[length];
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        fileStream.Seek(offset, SeekOrigin.Begin);
        
        var bytesRead = await fileStream.ReadAsync(buffer, 0, length, cancellationToken);
        
        _logger?.LogDebug("Read {BytesRead} bytes using FileStream from {FilePath} at offset {Offset}", 
            bytesRead, filePath, offset);
        
        return new ReadOnlyMemory<byte>(buffer, 0, bytesRead);
    }

    private async Task WriteRawAsync(string location, long offset, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(location);
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        fileStream.Seek(offset, SeekOrigin.Begin);
        
        await fileStream.WriteAsync(data, cancellationToken);
        
        // If we're writing at offset 0, truncate the file to the new data length
        // This ensures we don't leave old data beyond the new content
        if (offset == 0)
        {
            fileStream.SetLength(data.Length);
        }
        
        await fileStream.FlushAsync(cancellationToken);
    }

    private async Task DeleteRawAsync(string location, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(location);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        await Task.CompletedTask;
    }

    private string GetFilePath(string location)
    {
        return Path.Combine(_basePath, location.Replace('/', Path.DirectorySeparatorChar));
    }


    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FileStorage));
        }
    }
}