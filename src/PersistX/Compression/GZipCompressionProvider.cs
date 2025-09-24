using System.Buffers;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;

namespace PersistX.Compression;

/// <summary>
/// GZip compression provider implementation.
/// </summary>
public class GZipCompressionProvider : ICompressionProvider
{
    private readonly ILogger<GZipCompressionProvider>? _logger;
    private readonly int _compressionLevel;

    public string Name => "GZip";
    public int CompressionLevel => _compressionLevel;

    public GZipCompressionProvider(ILogger<GZipCompressionProvider>? logger = null, int compressionLevel = 6)
    {
        _logger = logger;
        _compressionLevel = Math.Clamp(compressionLevel, 0, 9);
    }

    public async Task<ReadOnlyMemory<byte>> CompressAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default)
    {
        if (input.Length == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        try
        {
            _logger?.LogDebug("Compressing data with compression level: {CompressionLevel}", _compressionLevel);
            
            using var outputStream = new MemoryStream();
            var compressionLevel = _compressionLevel switch
            {
                0 => System.IO.Compression.CompressionLevel.NoCompression,
                1 => System.IO.Compression.CompressionLevel.Fastest,
                2 => System.IO.Compression.CompressionLevel.Optimal,
                3 => System.IO.Compression.CompressionLevel.SmallestSize,
                _ => System.IO.Compression.CompressionLevel.Optimal // Default to Optimal for values 4-9
            };
            
            _logger?.LogDebug("Mapped compression level {OriginalLevel} to {MappedLevel}", _compressionLevel, compressionLevel);
            using var gzipStream = new GZipStream(outputStream, compressionLevel, leaveOpen: true);
            
            await gzipStream.WriteAsync(input, cancellationToken);
            await gzipStream.FlushAsync(cancellationToken);
            gzipStream.Close();

            return outputStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to compress data using GZip");
            throw;
        }
    }

    public async Task<ReadOnlyMemory<byte>> DecompressAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default)
    {
        if (input.Length == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        try
        {
            using var inputStream = new MemoryStream(input.ToArray());
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();

            await gzipStream.CopyToAsync(outputStream, cancellationToken);
            
            return outputStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to decompress data using GZip");
            throw;
        }
    }

    public double EstimateCompressionRatio(int inputSize)
    {
        // GZip typically achieves 60-80% compression for text data, 10-30% for binary data
        // This is a rough estimate - actual compression depends on data characteristics
        return inputSize < 1024 ? 0.7 : 0.5;
    }

    public int GetMaxCompressedSize(int inputSize)
    {
        // GZip worst case is roughly 103% of original size + 18 bytes overhead
        return (int)(inputSize * 1.03) + 18;
    }
}
