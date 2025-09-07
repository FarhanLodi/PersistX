using System.Buffers;

namespace PersistX.Interfaces;

/// <summary>
/// Represents a serializer for converting objects to/from binary data.
/// </summary>
/// <typeparam name="T">The type to serialize</typeparam>
public interface ISerializer<T>
{
    /// <summary>
    /// Gets the format identifier for this serializer.
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Serializes an object to binary data.
    /// </summary>
    /// <param name="value">The object to serialize</param>
    /// <param name="buffer">Buffer to write serialized data to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of bytes written</returns>
    Task<int> SerializeAsync(T value, IBufferWriter<byte> buffer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes binary data to an object.
    /// </summary>
    /// <param name="data">The binary data to deserialize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The deserialized object</returns>
    Task<T> DeserializeAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the estimated serialized size of an object.
    /// </summary>
    /// <param name="value">The object to estimate size for</param>
    /// <returns>Estimated size in bytes</returns>
    int GetEstimatedSize(T value);

    /// <summary>
    /// Checks if this serializer can handle the specified type.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type is supported</returns>
    bool CanSerialize(Type type);
}

/// <summary>
/// Represents a type-agnostic serializer.
/// </summary>
public interface ISerializer
{
    /// <summary>
    /// Gets the format identifier for this serializer.
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Serializes an object to binary data.
    /// </summary>
    /// <param name="value">The object to serialize</param>
    /// <param name="buffer">Buffer to write serialized data to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of bytes written</returns>
    Task<int> SerializeAsync(object value, IBufferWriter<byte> buffer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes binary data to an object of the specified type.
    /// </summary>
    /// <param name="data">The binary data to deserialize</param>
    /// <param name="type">The target type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The deserialized object</returns>
    Task<object> DeserializeAsync(ReadOnlyMemory<byte> data, Type type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the estimated serialized size of an object.
    /// </summary>
    /// <param name="value">The object to estimate size for</param>
    /// <returns>Estimated size in bytes</returns>
    int GetEstimatedSize(object value);

    /// <summary>
    /// Checks if this serializer can handle the specified type.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type is supported</returns>
    bool CanSerialize(Type type);
}

/// <summary>
/// Serialization format identifiers.
/// </summary>
public static class SerializationFormats
{
    /// <summary>
    /// JSON format.
    /// </summary>
    public const string Json = "json";

    /// <summary>
    /// MessagePack format.
    /// </summary>
    public const string MessagePack = "messagepack";

    /// <summary>
    /// Protocol Buffers format.
    /// </summary>
    public const string Protobuf = "protobuf";

    /// <summary>
    /// Binary format.
    /// </summary>
    public const string Binary = "binary";

    /// <summary>
    /// Custom format.
    /// </summary>
    public const string Custom = "custom";
}
