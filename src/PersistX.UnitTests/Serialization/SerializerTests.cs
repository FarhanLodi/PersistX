using System.Buffers;
using PersistX.Serialization;
using Xunit;

namespace PersistX.UnitTests.Serialization;

public record TestData(string Name, int Age, List<string> Tags);

public class SerializerTests
{
    // -------------------------------------------------------------------------
    // JsonSerializer<T>
    // -------------------------------------------------------------------------

    [Fact]
    public async Task JsonSerializer_SerializeDeserialize_ValuesMatch()
    {
        var serializer = new JsonSerializer<TestData>();
        var original = new TestData("Alice", 30, new List<string> { "dev", "manager" });

        var buffer = new ArrayBufferWriter<byte>();
        await serializer.SerializeAsync(original, buffer);
        var bytes = buffer.WrittenMemory;

        var deserialized = await serializer.DeserializeAsync(bytes);

        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Age, deserialized.Age);
        Assert.Equal(original.Tags, deserialized.Tags);
    }

    [Fact]
    public async Task JsonSerializer_FormatIsJson()
    {
        var serializer = new JsonSerializer<TestData>();
        Assert.Equal("json", serializer.Format);
    }

    [Fact]
    public async Task JsonSerializer_CanSerialize_CommonTypes()
    {
        var serializer = new JsonSerializer<TestData>();
        Assert.True(serializer.CanSerialize(typeof(TestData)));
        Assert.True(serializer.CanSerialize(typeof(string)));
        Assert.True(serializer.CanSerialize(typeof(int)));
    }

    [Fact]
    public async Task JsonSerializer_GetEstimatedSize_PositiveForNonEmpty()
    {
        var serializer = new JsonSerializer<TestData>();
        var data = new TestData("Bob", 25, new List<string> { "tag1" });
        var size = serializer.GetEstimatedSize(data);
        Assert.True(size > 0);
    }

    // -------------------------------------------------------------------------
    // CompressedJsonSerializer<T>
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CompressedJsonSerializer_SerializeDeserialize_ValuesMatch()
    {
        var serializer = new CompressedJsonSerializer<TestData>();
        var original = new TestData("Bob", 25, new List<string> { "a", "b", "c" });

        var buffer = new ArrayBufferWriter<byte>();
        await serializer.SerializeAsync(original, buffer);
        var bytes = buffer.WrittenMemory;

        var deserialized = await serializer.DeserializeAsync(bytes);

        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Age, deserialized.Age);
        Assert.Equal(original.Tags, deserialized.Tags);
    }

    [Fact]
    public async Task CompressedJsonSerializer_CompressedOutputSmallerThanJson_ForLargeObjects()
    {
        var jsonSerializer = new JsonSerializer<List<TestData>>();
        var compressedSerializer = new CompressedJsonSerializer<List<TestData>>();

        // Build a large, repetitive list that compresses well
        var largeList = Enumerable.Range(0, 100)
            .Select(i => new TestData(
                $"Employee{i:D3}",
                20 + (i % 40),
                new List<string> { "engineering", "backend", "dotnet", $"team{i % 5}" }))
            .ToList();

        var jsonBuffer = new ArrayBufferWriter<byte>();
        await jsonSerializer.SerializeAsync(largeList, jsonBuffer);
        var jsonSize = jsonBuffer.WrittenCount;

        var compressedBuffer = new ArrayBufferWriter<byte>();
        await compressedSerializer.SerializeAsync(largeList, compressedBuffer);
        var compressedSize = compressedBuffer.WrittenCount;

        Assert.True(compressedSize < jsonSize,
            $"Expected compressed ({compressedSize}) < json ({jsonSize})");
    }

    [Fact]
    public async Task CompressedJsonSerializer_FormatIsJson()
    {
        var serializer = new CompressedJsonSerializer<TestData>();
        Assert.Equal("json", serializer.Format);
    }

    // -------------------------------------------------------------------------
    // MessagePackSerializer<T>
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MessagePackSerializer_SerializeDeserialize_ValuesMatch()
    {
        var serializer = new MessagePackSerializer<TestData>();
        var original = new TestData("Charlie", 35, new List<string> { "x", "y" });

        var buffer = new ArrayBufferWriter<byte>();
        await serializer.SerializeAsync(original, buffer);
        var bytes = buffer.WrittenMemory;

        var deserialized = await serializer.DeserializeAsync(bytes);

        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Age, deserialized.Age);
        Assert.Equal(original.Tags, deserialized.Tags);
    }

    [Fact]
    public async Task MessagePackSerializer_FormatIsMessagePack()
    {
        var serializer = new MessagePackSerializer<TestData>();
        Assert.Equal("messagepack", serializer.Format);
    }

    [Fact]
    public async Task MessagePackSerializer_SerializedSizeSmaller_ThanJson()
    {
        var jsonSerializer = new JsonSerializer<TestData>();
        var msgpackSerializer = new MessagePackSerializer<TestData>();

        var data = new TestData(
            "SomeLongNameThatMakesSizeComparable",
            42,
            new List<string> { "tag1", "tag2", "tag3", "tag4", "tag5" });

        var jsonBuffer = new ArrayBufferWriter<byte>();
        await jsonSerializer.SerializeAsync(data, jsonBuffer);

        var mpBuffer = new ArrayBufferWriter<byte>();
        await msgpackSerializer.SerializeAsync(data, mpBuffer);

        // MessagePack is typically more compact than JSON for structured data
        Assert.True(mpBuffer.WrittenCount < jsonBuffer.WrittenCount,
            $"MessagePack ({mpBuffer.WrittenCount}) should be smaller than JSON ({jsonBuffer.WrittenCount})");
    }
}
