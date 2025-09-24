using System.Buffers;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;

namespace PersistX.Compression;

/// <summary>
/// Deflate compression provider implementation.
/// </summary>
public class DeflateCompressionProvider : ICompressionProvider
{
    private readonly ILogger<DeflateCompressionProvider>? _logger;
    private readonly int _compressionLevel;

    public string Name => "Deflate";
    public int CompressionLevel => _compressionLevel;

    public DeflateCompressionProvider(ILogger<DeflateCompressionProvider>? logger = null, int compressionLevel = 6)
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
            using var outputStream = new MemoryStream();
            var compressionLevel = _compressionLevel switch
            {
                0 => System.IO.Compression.CompressionLevel.NoCompression,
                1 => System.IO.Compression.CompressionLevel.Fastest,
                2 => System.IO.Compression.CompressionLevel.Optimal,
                3 => System.IO.Compression.CompressionLevel.SmallestSize,
                _ => System.IO.Compression.CompressionLevel.Optimal // Default to Optimal for values 4-9
            };
            using var deflateStream = new DeflateStream(outputStream, compressionLevel, leaveOpen: true);
            
            await deflateStream.WriteAsync(input, cancellationToken);
            await deflateStream.FlushAsync(cancellationToken);
            deflateStream.Close();

            return outputStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to compress data using Deflate");
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
            using var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();

            await deflateStream.CopyToAsync(outputStream, cancellationToken);
            
            return outputStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to decompress data using Deflate");
            throw;
        }
    }

    public double EstimateCompressionRatio(int inputSize)
    {
        // Deflate typically achieves similar compression to GZip but with less overhead
        return inputSize < 1024 ? 0.75 : 0.55;
    }

    public int GetMaxCompressedSize(int inputSize)
    {
        // Deflate worst case is roughly 103% of original size + 5 bytes overhead
        return (int)(inputSize * 1.03) + 5;
    }
}
