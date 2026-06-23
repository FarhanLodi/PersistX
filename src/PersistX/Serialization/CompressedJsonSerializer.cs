using System.Buffers;
using System.IO.Compression;
using System.Text.Json;
using PersistX.Interfaces;

namespace PersistX.Serialization;

/// <summary>
/// JSON serializer with GZip compression. Well-suited for large objects where the compression
/// ratio offsets the CPU cost of deflating/inflating.
/// </summary>
/// <typeparam name="T">The type to serialize</typeparam>
public class CompressedJsonSerializer<T> : ISerializer<T>
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly CompressionLevel _compressionLevel;

    /// <summary>
    /// Default instance using camelCase JSON naming and <see cref="CompressionLevel.Optimal"/>.
    /// </summary>
    public static readonly CompressedJsonSerializer<T> Default = new();

    /// <param name="jsonOptions">
    /// JSON serializer options. Defaults to camelCase naming with no indentation.
    /// </param>
    /// <param name="compressionLevel">
    /// GZip compression level. Defaults to <see cref="CompressionLevel.Optimal"/>.
    /// </param>
    public CompressedJsonSerializer(
        JsonSerializerOptions? jsonOptions = null,
        CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _compressionLevel = compressionLevel;
    }

    /// <inheritdoc/>
    public string Format => SerializationFormats.Json;

    /// <inheritdoc/>
    public async Task<int> SerializeAsync(T value, IBufferWriter<byte> buffer, CancellationToken cancellationToken = default)
    {
        // Serialize to JSON, then GZip-compress into a MemoryStream, then copy compressed
        // bytes into the caller's IBufferWriter<byte>.
        using var ms = new MemoryStream();
        await using (var gzip = new GZipStream(ms, _compressionLevel, leaveOpen: true))
        {
            await System.Text.Json.JsonSerializer.SerializeAsync(gzip, value, _jsonOptions, cancellationToken);
        }

        var compressed = ms.ToArray();
        var span = buffer.GetSpan(compressed.Length);
        compressed.CopyTo(span);
        buffer.Advance(compressed.Length);

        return compressed.Length;
    }

    /// <inheritdoc/>
    public async Task<T> DeserializeAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream(data.ToArray());
        await using var gzip = new GZipStream(ms, CompressionMode.Decompress);
        var result = await System.Text.Json.JsonSerializer.DeserializeAsync<T>(gzip, _jsonOptions, cancellationToken);
        return result ?? throw new InvalidDataException("Deserialization returned null.");
    }

    /// <inheritdoc/>
    public int GetEstimatedSize(T value)
    {
        // Serialize to JSON bytes first, then compress, to get the true compressed size.
        var jsonBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value, _jsonOptions);
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, _compressionLevel, leaveOpen: true))
        {
            gzip.Write(jsonBytes, 0, jsonBytes.Length);
        }
        return (int)ms.Length;
    }

    /// <inheritdoc/>
    public bool CanSerialize(Type type)
    {
        // System.Text.Json supports most managed types; exclude unrepresentable native types.
        return type != typeof(IntPtr) &&
               type != typeof(UIntPtr) &&
               !type.IsPointer;
    }
}
