using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;

namespace PersistX.Storage;

/// <summary>
/// In-memory storage for PersistX.
/// </summary>
public class MemoryStorage : IBackend
{
    private readonly ILogger<MemoryStorage>? _logger;
    private readonly ConcurrentDictionary<string, byte[]> _storage = new();
    private bool _disposed;

    public string Name => "MemoryStorage";

    public MemoryStorage(ILogger<MemoryStorage>? logger = null)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(IBackendConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("MemoryStorage initialized");
        await Task.CompletedTask;
    }

    public async Task<ReadOnlyMemory<byte>> ReadAsync(string location, long offset, int length, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!_storage.TryGetValue(location, out var data))
        {
            throw new KeyNotFoundException($"Location not found: {location}");
        }

        if (offset >= data.Length)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var actualLength = Math.Min(length, data.Length - (int)offset);
        var result = new byte[actualLength];
        Array.Copy(data, offset, result, 0, actualLength);

        return await Task.FromResult(new ReadOnlyMemory<byte>(result));
    }

    public async Task WriteAsync(string location, long offset, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        _storage.AddOrUpdate(location, 
            key => 
            {
                var newData = new byte[offset + data.Length];
                data.CopyTo(newData.AsMemory((int)offset));
                return newData;
            },
            (key, existingData) =>
            {
                var newLength = Math.Max(existingData.Length, (int)offset + data.Length);
                var newData = new byte[newLength];
                Array.Copy(existingData, newData, existingData.Length);
                data.CopyTo(newData.AsMemory((int)offset));
                return newData;
            });

        await Task.CompletedTask;
    }

    public async Task DeleteAsync(string location, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _storage.TryRemove(location, out _);
        await Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string location, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await Task.FromResult(_storage.ContainsKey(location));
    }

    public async Task<long> GetSizeAsync(string location, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!_storage.TryGetValue(location, out var data))
        {
            return -1;
        }

        return await Task.FromResult(data.Length);
    }

    public async IAsyncEnumerable<string> ListLocationsAsync(string? pattern = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var locations = _storage.Keys.ToList();
        
        if (!string.IsNullOrEmpty(pattern))
        {
            // Simple pattern matching - could be enhanced with proper glob patterns
            locations = locations.Where(loc => loc.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        foreach (var location in locations)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            yield return location;
            await Task.Yield(); // Allow cancellation
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        // No-op for in-memory backend
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _storage.Clear();
        await Task.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MemoryStorage));
        }
    }
}
