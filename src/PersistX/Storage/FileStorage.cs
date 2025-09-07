using System.Collections.Concurrent;
using System.IO;
using System.IO.MemoryMappedFiles;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;

namespace PersistX.Storage;

/// <summary>
/// File-based storage for PersistX.
/// </summary>
public class FileStorage : IBackend
{
    private readonly ILogger<FileStorage>? _logger;
    private readonly ConcurrentDictionary<string, FileStream> _openFiles = new();
    private readonly ConcurrentDictionary<string, MemoryMappedFile> _memoryMappedFiles = new();
    private string _basePath = string.Empty;
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

        _logger?.LogInformation("FileStorage initialized with base path: {BasePath}", _basePath);
        await Task.CompletedTask;
    }

    public async Task<ReadOnlyMemory<byte>> ReadAsync(string location, long offset, int length, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var filePath = GetFilePath(location);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var buffer = new byte[length];
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        fileStream.Seek(offset, SeekOrigin.Begin);
        
        var bytesRead = await fileStream.ReadAsync(buffer, 0, length, cancellationToken);
        
        return new ReadOnlyMemory<byte>(buffer, 0, bytesRead);
    }

    public async Task WriteAsync(string location, long offset, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
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
        await fileStream.FlushAsync(cancellationToken);
    }

    public async Task DeleteAsync(string location, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var filePath = GetFilePath(location);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        await Task.CompletedTask;
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
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Close all open files
        foreach (var fileStream in _openFiles.Values)
        {
            await fileStream.DisposeAsync();
        }
        _openFiles.Clear();

        // Dispose memory mapped files
        foreach (var mmf in _memoryMappedFiles.Values)
        {
            mmf.Dispose();
        }
        _memoryMappedFiles.Clear();

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
