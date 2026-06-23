using PersistX.FileBased;
using Xunit;

namespace PersistX.UnitTests.FileBased;

public class PersistentStackTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public PersistentStackTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "persistx_stack_" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "stack.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Push_Pop_LifoOrder()
    {
        var stack = new PersistentStack<string>(_path);
        await stack.PushAsync("first");
        await stack.PushAsync("second");
        await stack.PushAsync("third");

        var top = await stack.PopAsync();
        var mid = await stack.PopAsync();
        var bot = await stack.PopAsync();

        Assert.Equal("third", top);
        Assert.Equal("second", mid);
        Assert.Equal("first", bot);
    }

    [Fact]
    public async Task PeekAsync_DoesNotRemoveItem()
    {
        var stack = new PersistentStack<string>(_path);
        await stack.PushAsync("peek-me");

        var peeked = await stack.PeekAsync();
        var countAfterPeek = await stack.CountAsync();

        Assert.Equal("peek-me", peeked);
        Assert.Equal(1, countAfterPeek);
    }

    [Fact]
    public async Task CountAsync_AccurateAfterPushAndPop()
    {
        var stack = new PersistentStack<int>(_path);
        await stack.PushAsync(1);
        await stack.PushAsync(2);
        await stack.PushAsync(3);

        Assert.Equal(3, await stack.CountAsync());

        await stack.PopAsync();
        Assert.Equal(2, await stack.CountAsync());
    }

    [Fact]
    public async Task ClearAsync_EmptiesStack()
    {
        var stack = new PersistentStack<string>(_path);
        await stack.PushAsync("a");
        await stack.PushAsync("b");
        await stack.ClearAsync();

        Assert.Equal(0, await stack.CountAsync());
        Assert.True(await stack.IsEmptyAsync());
    }

    [Fact]
    public async Task PopAsync_OnEmptyStack_ReturnsDefault()
    {
        var stack = new PersistentStack<string>(_path);
        var item = await stack.PopAsync();
        Assert.Null(item);
    }

    [Fact]
    public async Task Persistence_DataSurvivesNewInstance()
    {
        var stack1 = new PersistentStack<string>(_path);
        await stack1.PushAsync("bottom");
        await stack1.PushAsync("top");

        var stack2 = new PersistentStack<string>(_path);
        var count = await stack2.CountAsync();
        var top = await stack2.PopAsync();

        Assert.Equal(2, count);
        Assert.Equal("top", top);
    }

    [Fact]
    public async Task PushRange_ThenPopInReverseOrder()
    {
        var stack = new PersistentStack<int>(_path);
        // PushRangeAsync pushes items in sequence, last pushed = top
        await stack.PushRangeAsync(new[] { 1, 2, 3, 4, 5 });

        var results = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            var item = await stack.PopAsync();
            results.Add((int)item!);
        }

        // Last item pushed (5) should come out first
        Assert.Equal(5, results[0]);
        Assert.Equal(1, results[4]);
    }

    [Fact]
    public async Task IsEmptyAsync_TrueWhenEmpty()
    {
        var stack = new PersistentStack<string>(_path);
        Assert.True(await stack.IsEmptyAsync());
    }

    [Fact]
    public async Task IsEmptyAsync_FalseWhenNotEmpty()
    {
        var stack = new PersistentStack<string>(_path);
        await stack.PushAsync("item");
        Assert.False(await stack.IsEmptyAsync());
    }

    [Fact]
    public async Task ContainsAsync_ExistingItem_ReturnsTrue()
    {
        var stack = new PersistentStack<string>(_path);
        await stack.PushAsync("find-me");
        Assert.True(await stack.ContainsAsync("find-me"));
    }

    [Fact]
    public async Task ContainsAsync_MissingItem_ReturnsFalse()
    {
        var stack = new PersistentStack<string>(_path);
        await stack.PushAsync("other");
        Assert.False(await stack.ContainsAsync("not-here"));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsItemsLifoOrder()
    {
        var stack = new PersistentStack<string>(_path);
        await stack.PushAsync("a");
        await stack.PushAsync("b");
        await stack.PushAsync("c");

        var all = new List<string>();
        await foreach (var item in stack.GetAllAsync())
            all.Add(item);

        // LIFO: top to bottom
        Assert.Equal("c", all[0]);
        Assert.Equal("b", all[1]);
        Assert.Equal("a", all[2]);
    }
}
