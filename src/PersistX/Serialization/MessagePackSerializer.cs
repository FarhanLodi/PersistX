using System.Buffers;
using MessagePack;
using MessagePack.Resolvers;
using PersistX.Interfaces;

namespace PersistX.Serialization;

/// <summary>
/// High-performance MessagePack serializer. Uses ContractlessStandardResolver so T does not
/// need [MessagePackObject] attributes. Pass custom options to opt into attribute-based contracts.
/// </summary>
/// <typeparam name="T">The type to serialize</typeparam>
public class MessagePackSerializer<T> : ISerializer<T>
{
    private readonly MessagePackSerializerOptions _options;

    /// <summary>
    /// Default instance using <see cref="ContractlessStandardResolver"/>.
    /// </summary>
    public static readonly MessagePackSerializer<T> Default = new();

    /// <param name="options">
    /// Custom MessagePack options. Defaults to <see cref="MessagePackSerializerOptions.Standard"/>
    /// with <see cref="ContractlessStandardResolver"/> so attribute annotations are not required.
    /// </param>
    public MessagePackSerializer(MessagePackSerializerOptions? options = null)
    {
        _options = options ?? MessagePackSerializerOptions.Standard
            .WithResolver(ContractlessStandardResolver.Instance);
    }

    /// <inheritdoc/>
    public string Format => SerializationFormats.MessagePack;

    /// <inheritdoc/>
    public Task<int> SerializeAsync(T value, IBufferWriter<byte> buffer, CancellationToken cancellationToken = default)
    {
        // MessagePack.MessagePackSerializer.Serialize writes directly into the IBufferWriter<byte>.
        // Capture the position before and after to compute how many bytes were written.
        var countingWriter = new ByteCountingBufferWriter(buffer);
        MessagePack.MessagePackSerializer.Serialize(countingWriter, value, _options, cancellationToken);
        return Task.FromResult(countingWriter.BytesWritten);
    }

    /// <inheritdoc/>
    public Task<T> DeserializeAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var result = MessagePack.MessagePackSerializer.Deserialize<T>(data, _options, cancellationToken);
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public int GetEstimatedSize(T value)
    {
        return MessagePack.MessagePackSerializer.Serialize(value, _options).Length;
    }

    /// <inheritdoc/>
    public bool CanSerialize(Type type)
    {
        // MessagePack with ContractlessStandardResolver supports all public types except pointers
        // and native-integer types that have no managed representation.
        return type != typeof(IntPtr) &&
               type != typeof(UIntPtr) &&
               !type.IsPointer;
    }

    // ---------------------------------------------------------------------------
    // Helper: wraps an IBufferWriter<byte> and tracks the total bytes advanced.
    // ---------------------------------------------------------------------------
    private sealed class ByteCountingBufferWriter : IBufferWriter<byte>
    {
        private readonly IBufferWriter<byte> _inner;

        public int BytesWritten { get; private set; }

        public ByteCountingBufferWriter(IBufferWriter<byte> inner)
        {
            _inner = inner;
        }

        public void Advance(int count)
        {
            _inner.Advance(count);
            BytesWritten += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0) => _inner.GetMemory(sizeHint);

        public Span<byte> GetSpan(int sizeHint = 0) => _inner.GetSpan(sizeHint);
    }
}
