using PersistX.Collections;
using PersistX.Events;
using PersistX.Storage;
using PersistX.UnitTests;
using Xunit;

namespace PersistX.UnitTests.Events;

public class ObservableCollectionDecoratorTests : IAsyncLifetime
{
    private MemoryStorage _storage = null!;
    private PersistentCollection<string> _inner = null!;
    private ObservableCollectionDecorator<string> _decorated = null!;

    public async Task InitializeAsync()
    {
        _storage = new MemoryStorage();
        await _storage.InitializeAsync(new TestBackendConfig());

        _inner = new PersistentCollection<string>("test_col", _storage);
        await _inner.InitializeAsync();

        _decorated = new ObservableCollectionDecorator<string>(_inner);
    }

    public async Task DisposeAsync()
    {
        await _decorated.DisposeAsync();
        await _storage.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_PublishesAddedChangeEvent()
    {
        await _decorated.AddAsync("hello");

        var hasItem = _decorated.Changes.Reader.TryRead(out var change);

        Assert.True(hasItem);
        Assert.Equal(CollectionChangeType.Added, change!.ChangeType);
        Assert.Equal("hello", change.Item);
    }

    [Fact]
    public async Task RemoveAsync_PublishesRemovedEvent()
    {
        await _decorated.AddAsync("remove-me");
        // drain the Added event
        _decorated.Changes.Reader.TryRead(out _);

        var removed = await _decorated.RemoveAsync("remove-me");
        var hasItem = _decorated.Changes.Reader.TryRead(out var change);

        Assert.True(removed);
        Assert.True(hasItem);
        Assert.Equal(CollectionChangeType.Removed, change!.ChangeType);
        Assert.Equal("remove-me", change.Item);
    }

    [Fact]
    public async Task ClearAsync_PublishesClearedEvent()
    {
        await _decorated.AddAsync("item1");
        await _decorated.AddAsync("item2");
        // drain Added events
        _decorated.Changes.Reader.TryRead(out _);
        _decorated.Changes.Reader.TryRead(out _);

        await _decorated.ClearAsync();

        var hasItem = _decorated.Changes.Reader.TryRead(out var change);

        Assert.True(hasItem);
        Assert.Equal(CollectionChangeType.Cleared, change!.ChangeType);
    }

    [Fact]
    public async Task WatchAsync_StreamsChangesAsTheyHappen()
    {
        var receivedChanges = new List<CollectionChange<string>>();
        using var cts = new CancellationTokenSource();

        // Start watching in background
        var watchTask = Task.Run(async () =>
        {
            await foreach (var change in _decorated.WatchAsync(cts.Token))
            {
                receivedChanges.Add(change);
                if (receivedChanges.Count >= 2)
                    break;
            }
        });

        await Task.Delay(50); // let the watch loop start
        await _decorated.AddAsync("first");
        await _decorated.AddAsync("second");

        // Wait for both changes to be received
        await watchTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, receivedChanges.Count);
        Assert.All(receivedChanges, c => Assert.Equal(CollectionChangeType.Added, c.ChangeType));
    }

    [Fact]
    public async Task Changes_HasCorrectCollectionName()
    {
        await _decorated.AddAsync("test");

        _decorated.Changes.Reader.TryRead(out var change);

        Assert.Equal("test_col", change!.CollectionName);
    }

    [Fact]
    public async Task AddRangeAsync_PublishesRangeAddedEvent()
    {
        var items = new[] { "a", "b", "c" };
        await _decorated.AddRangeAsync(items);

        _decorated.Changes.Reader.TryRead(out var change);

        Assert.NotNull(change);
        Assert.Equal(CollectionChangeType.RangeAdded, change!.ChangeType);
        Assert.Equal(3, change.AffectedCount);
    }

    [Fact]
    public async Task RemoveAsync_NonExistentItem_DoesNotPublishEvent()
    {
        await _decorated.AddAsync("existing");
        // drain the Added event
        _decorated.Changes.Reader.TryRead(out _);

        var removed = await _decorated.RemoveAsync("non-existent");

        Assert.False(removed);
        var hasChange = _decorated.Changes.Reader.TryRead(out _);
        Assert.False(hasChange);
    }

    [Fact]
    public async Task Name_DelegatesToInner()
    {
        Assert.Equal("test_col", _decorated.Name);
    }

    [Fact]
    public async Task CountAsync_DelegatesToInner()
    {
        await _decorated.AddAsync("one");
        await _decorated.AddAsync("two");

        var count = await _decorated.CountAsync;
        Assert.Equal(2, count);
    }
}
