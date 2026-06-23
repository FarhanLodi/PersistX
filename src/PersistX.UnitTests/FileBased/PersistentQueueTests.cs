using PersistX.FileBased;
using Xunit;

namespace PersistX.UnitTests.FileBased;

public class PersistentQueueTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public PersistentQueueTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "persistx_queue_" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "queue.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Enqueue_Dequeue_FifoOrder()
    {
        var queue = new PersistentQueue<string>(_path);
        await queue.EnqueueAsync("first");
        await queue.EnqueueAsync("second");
        await queue.EnqueueAsync("third");

        var a = await queue.DequeueAsync();
        var b = await queue.DequeueAsync();
        var c = await queue.DequeueAsync();

        Assert.Equal("first", a);
        Assert.Equal("second", b);
        Assert.Equal("third", c);
    }

    [Fact]
    public async Task PeekAsync_DoesNotRemoveItem()
    {
        var queue = new PersistentQueue<string>(_path);
        await queue.EnqueueAsync("peek-me");

        var peeked = await queue.PeekAsync();
        var countAfterPeek = await queue.CountAsync();

        Assert.Equal("peek-me", peeked);
        Assert.Equal(1, countAfterPeek);
    }

    [Fact]
    public async Task CountAsync_AccurateAfterEnqueueAndDequeue()
    {
        var queue = new PersistentQueue<int>(_path);
        await queue.EnqueueAsync(1);
        await queue.EnqueueAsync(2);
        await queue.EnqueueAsync(3);

        Assert.Equal(3, await queue.CountAsync());

        await queue.DequeueAsync();
        Assert.Equal(2, await queue.CountAsync());
    }

    [Fact]
    public async Task ClearAsync_EmptiesQueue()
    {
        var queue = new PersistentQueue<string>(_path);
        await queue.EnqueueAsync("a");
        await queue.EnqueueAsync("b");
        await queue.ClearAsync();

        Assert.Equal(0, await queue.CountAsync());
        Assert.True(await queue.IsEmptyAsync());
    }

    [Fact]
    public async Task TryDequeueAsync_OnEmptyQueue_ReturnsFalseAndDefault()
    {
        var queue = new PersistentQueue<string>(_path);
        var (success, item) = await queue.TryDequeueAsync();

        Assert.False(success);
        Assert.Null(item);
    }

    [Fact]
    public async Task TryDequeueAsync_OnNonEmptyQueue_ReturnsTrueAndItem()
    {
        var queue = new PersistentQueue<string>(_path);
        await queue.EnqueueAsync("value");
        var (success, item) = await queue.TryDequeueAsync();

        Assert.True(success);
        Assert.Equal("value", item);
    }

    [Fact]
    public async Task Persistence_DataSurvivesNewInstance()
    {
        var queue1 = new PersistentQueue<string>(_path);
        await queue1.EnqueueAsync("one");
        await queue1.EnqueueAsync("two");

        var queue2 = new PersistentQueue<string>(_path);
        var count = await queue2.CountAsync();
        var first = await queue2.DequeueAsync();

        Assert.Equal(2, count);
        Assert.Equal("one", first);
    }

    [Fact]
    public async Task EnqueueRange_ThenDequeueAll_InOrder()
    {
        var queue = new PersistentQueue<int>(_path);
        await queue.EnqueueRangeAsync(new[] { 10, 20, 30, 40, 50 });

        var results = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            var item = await queue.DequeueAsync();
            results.Add((int)item!);
        }

        Assert.Equal(new[] { 10, 20, 30, 40, 50 }, results);
    }

    [Fact]
    public async Task IsEmptyAsync_TrueWhenEmpty()
    {
        var queue = new PersistentQueue<string>(_path);
        Assert.True(await queue.IsEmptyAsync());
    }

    [Fact]
    public async Task IsEmptyAsync_FalseWhenNotEmpty()
    {
        var queue = new PersistentQueue<string>(_path);
        await queue.EnqueueAsync("item");
        Assert.False(await queue.IsEmptyAsync());
    }

    [Fact]
    public async Task ContainsAsync_ExistingItem_ReturnsTrue()
    {
        var queue = new PersistentQueue<string>(_path);
        await queue.EnqueueAsync("find-me");
        Assert.True(await queue.ContainsAsync("find-me"));
    }

    [Fact]
    public async Task ContainsAsync_MissingItem_ReturnsFalse()
    {
        var queue = new PersistentQueue<string>(_path);
        await queue.EnqueueAsync("something-else");
        Assert.False(await queue.ContainsAsync("not-here"));
    }

    [Fact]
    public async Task DequeueAsync_OnEmptyQueue_ReturnsDefault()
    {
        var queue = new PersistentQueue<string>(_path);
        var item = await queue.DequeueAsync();
        Assert.Null(item);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllItemsInOrder()
    {
        var queue = new PersistentQueue<string>(_path);
        await queue.EnqueueRangeAsync(new[] { "alpha", "beta", "gamma" });

        var all = new List<string>();
        await foreach (var item in queue.GetAllAsync())
            all.Add(item);

        Assert.Equal(new[] { "alpha", "beta", "gamma" }, all);
    }
}
