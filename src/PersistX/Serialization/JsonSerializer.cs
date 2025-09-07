using System.Buffers;
using System.Text.Json;
using PersistX.Interfaces;

namespace PersistX.Serialization;

/// <summary>
/// JSON-based serializer implementation.
/// </summary>
/// <typeparam name="T">The type to serialize</typeparam>
public class JsonSerializer<T> : ISerializer<T>
{
    private readonly JsonSerializerOptions _options;

    public string Format => SerializationFormats.Json;

    public JsonSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<int> SerializeAsync(T value, IBufferWriter<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await System.Text.Json.JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken);
        
        var data = stream.ToArray();
        buffer.Write(data);
        
        return data.Length;
    }

    public Task<T> DeserializeAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var result = System.Text.Json.JsonSerializer.Deserialize<T>(data.Span, _options);
        return Task.FromResult(result!);
    }

    public int GetEstimatedSize(T value)
    {
        // Rough estimation - JSON is typically 1.5-2x the size of binary data
        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value, _options).Length;
    }

    public bool CanSerialize(Type type)
    {
        // JSON can serialize most types, but we'll be conservative
        return type != typeof(IntPtr) && 
               type != typeof(UIntPtr) && 
               !type.IsPointer;
    }
}

/// <summary>
/// Type-agnostic JSON serializer implementation.
/// </summary>
public class JsonSerializer : ISerializer
{
    private readonly JsonSerializerOptions _options;

    public string Format => SerializationFormats.Json;

    public JsonSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<int> SerializeAsync(object value, IBufferWriter<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await System.Text.Json.JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken);
        
        var data = stream.ToArray();
        buffer.Write(data);
        
        return data.Length;
    }

    public Task<object> DeserializeAsync(ReadOnlyMemory<byte> data, Type type, CancellationToken cancellationToken = default)
    {
        var result = System.Text.Json.JsonSerializer.Deserialize(data.Span, type, _options);
        return Task.FromResult(result!);
    }

    public int GetEstimatedSize(object value)
    {
        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value, _options).Length;
    }

    public bool CanSerialize(Type type)
    {
        return type != typeof(IntPtr) && 
               type != typeof(UIntPtr) && 
               !type.IsPointer;
    }
}
