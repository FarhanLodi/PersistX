using System.Buffers;

namespace PersistX.Interfaces;

/// <summary>
/// Represents a compression provider for data compression and decompression.
/// </summary>
public interface ICompressionProvider
{
    /// <summary>
    /// Gets the name of this compression algorithm.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the compression level (0-9, where 9 is maximum compression).
    /// </summary>
    int CompressionLevel { get; }

    /// <summary>
    /// Compresses the input data.
    /// </summary>
    /// <param name="input">Input data to compress</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Compressed data</returns>
    Task<ReadOnlyMemory<byte>> CompressAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decompresses the input data.
    /// </summary>
    /// <param name="input">Compressed data to decompress</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Decompressed data</returns>
    Task<ReadOnlyMemory<byte>> DecompressAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates the compression ratio for the given input size.
    /// </summary>
    /// <param name="inputSize">Size of input data in bytes</param>
    /// <returns>Estimated compression ratio (0.0 to 1.0)</returns>
    double EstimateCompressionRatio(int inputSize);

    /// <summary>
    /// Gets the maximum possible compressed size for the given input size.
    /// </summary>
    /// <param name="inputSize">Size of input data in bytes</param>
    /// <returns>Maximum compressed size in bytes</returns>
    int GetMaxCompressedSize(int inputSize);
}

/// <summary>
/// Configuration interface for compression providers.
/// </summary>
public interface ICompressionProviderConfiguration
{
    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration value, or null if not found</returns>
    string? GetValue(string key);

    /// <summary>
    /// Gets a configuration value by key with a default value.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if key not found</param>
    /// <returns>Configuration value or default</returns>
    string GetValue(string key, string defaultValue);

    /// <summary>
    /// Gets all configuration keys.
    /// </summary>
    /// <returns>Collection of configuration keys</returns>
    IEnumerable<string> GetKeys();
}
