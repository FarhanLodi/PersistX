using PersistX.Expiry;
using Xunit;

namespace PersistX.UnitTests.Expiry;

internal class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    public void Advance(TimeSpan amount) => _now = _now.Add(amount);
    public override DateTimeOffset GetUtcNow() => _now;
}

public class TtlCollectionTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public TtlCollectionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "persistx_ttl_" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "ttl.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task AddAsync_WithTtl_GetAllValidReturnsBeforeExpiry()
    {
        var clock = new FakeTimeProvider();
        var ttl = new TtlCollection<string>(_path, clock);

        await ttl.AddAsync("item1", TimeSpan.FromMinutes(10));

        var valid = await ttl.GetAllValidAsync();

        Assert.Single(valid);
        Assert.Equal("item1", valid[0]);
    }

    [Fact]
    public async Task AddAsync_ItemNotReturnedAfterTtlExpires()
    {
        var clock = new FakeTimeProvider();
        var ttl = new TtlCollection<string>(_path, clock);

        await ttl.AddAsync("expires-soon", TimeSpan.FromSeconds(5));

        // Advance time past TTL
        clock.Advance(TimeSpan.FromSeconds(10));

        var valid = await ttl.GetAllValidAsync();
        Assert.Empty(valid);
    }

    [Fact]
    public async Task PurgeExpiredAsync_RemovesExpiredItems_ReturnsCount()
    {
        var clock = new FakeTimeProvider();
        var ttl = new TtlCollection<string>(_path, clock);

        await ttl.AddAsync("short", TimeSpan.FromSeconds(1));
        await ttl.AddAsync("long", TimeSpan.FromHours(1));

        clock.Advance(TimeSpan.FromSeconds(5));

        var purged = await ttl.PurgeExpiredAsync();
        Assert.Equal(1, purged);

        var valid = await ttl.GetAllValidAsync();
        Assert.Single(valid);
        Assert.Equal("long", valid[0]);
    }

    [Fact]
    public async Task CountAsync_OnlyCountsNonExpiredItems()
    {
        var clock = new FakeTimeProvider();
        var ttl = new TtlCollection<string>(_path, clock);

        await ttl.AddAsync("live", TimeSpan.FromHours(1));
        await ttl.AddAsync("dead", TimeSpan.FromSeconds(1));

        clock.Advance(TimeSpan.FromSeconds(5));

        var count = await ttl.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ClearAsync_EmptiesEverything()
    {
        var clock = new FakeTimeProvider();
        var ttl = new TtlCollection<string>(_path, clock);

        await ttl.AddAsync("item1", TimeSpan.FromHours(1));
        await ttl.AddAsync("item2", TimeSpan.FromHours(2));

        await ttl.ClearAsync();

        var count = await ttl.TotalCountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetAsync_FindsItemByPredicate()
    {
        var clock = new FakeTimeProvider();
        var ttl = new TtlCollection<string>(_path, clock);

        await ttl.AddAsync("apple", TimeSpan.FromHours(1));
        await ttl.AddAsync("banana", TimeSpan.FromHours(1));

        var found = await ttl.GetAsync(s => s == "banana");
        Assert.Equal("banana", found);
    }

    [Fact]
    public async Task GetAsync_MissingItem_ReturnsNull()
    {
        var clock = new FakeTimeProvider();
        var ttl = new TtlCollection<string>(_path, clock);

        await ttl.AddAsync("apple", TimeSpan.FromHours(1));

        var found = await ttl.GetAsync(s => s == "not-there");
        Assert.Null(found);
    }

    [Fact]
    public async Task Persistence_AddItems_NewInstancePreservesIfNotExpired()
    {
        var clock = new FakeTimeProvider();
        var ttl1 = new TtlCollection<string>(_path, clock);

        await ttl1.AddAsync("persist-me", TimeSpan.FromHours(24));

        // New instance, same path, same clock (time not advanced)
        var ttl2 = new TtlCollection<string>(_path, clock);
        var valid = await ttl2.GetAllValidAsync();

        Assert.Single(valid);
        Assert.Equal("persist-me", valid[0]);
    }

    [Fact]
    public async Task AddAsync_WithAbsoluteExpiry_ReturnsBeforeExpiry()
    {
        var clock = new FakeTimeProvider();
        var ttl = new TtlCollection<string>(_path, clock);

        var expiresAt = clock.GetUtcNow().AddHours(2);
        await ttl.AddAsync("absolute-ttl", expiresAt);

        var valid = await ttl.GetAllValidAsync();
        Assert.Single(valid);
    }

    [Fact]
    public async Task TotalCountAsync_IncludesExpiredItems()
    {
        var clock = new FakeTimeProvider();
        var ttl = new TtlCollection<string>(_path, clock);

        await ttl.AddAsync("live", TimeSpan.FromHours(1));
        await ttl.AddAsync("expired", TimeSpan.FromSeconds(1));

        clock.Advance(TimeSpan.FromSeconds(5));

        var total = await ttl.TotalCountAsync();
        var active = await ttl.CountAsync();

        Assert.Equal(2, total);
        Assert.Equal(1, active);
    }

    [Fact]
    public async Task RemoveAsync_RemovesMatchingItem()
    {
        var clock = new FakeTimeProvider();
        var ttl = new TtlCollection<string>(_path, clock);

        await ttl.AddAsync("keep", TimeSpan.FromHours(1));
        await ttl.AddAsync("remove-me", TimeSpan.FromHours(1));

        var removed = await ttl.RemoveAsync(s => s == "remove-me");
        Assert.True(removed);

        var count = await ttl.CountAsync();
        Assert.Equal(1, count);
    }
}
