using PersistX.Collections;
using PersistX.Extensions;
using PersistX.Storage;
using PersistX.UnitTests;
using Xunit;

namespace PersistX.UnitTests.Extensions;

public record ScoredItem(string Name, int Score);

public class CollectionExtensionsTests : IAsyncLifetime
{
    private MemoryStorage _storage = null!;

    public async Task InitializeAsync()
    {
        _storage = new MemoryStorage();
        await _storage.InitializeAsync(new TestBackendConfig());
    }

    public async Task DisposeAsync()
    {
        await _storage.DisposeAsync();
    }

    private async Task<PersistentCollection<T>> CreateCollection<T>(string name, IEnumerable<T>? seed = null)
    {
        var col = new PersistentCollection<T>(name, _storage);
        await col.InitializeAsync();
        if (seed != null)
            await col.AddRangeAsync(seed);
        return col;
    }

    [Fact]
    public async Task ToListAsync_ReturnsAllItems()
    {
        var col = await CreateCollection("list_col", new[] { "alpha", "beta", "gamma" });
        var list = await col.ToListAsync();
        Assert.Equal(3, list.Count);
        Assert.Contains("alpha", list);
        Assert.Contains("beta", list);
        Assert.Contains("gamma", list);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task ToHashSetAsync_Deduplicates()
    {
        var col = await CreateCollection("hashset_col", new[] { "a", "b", "a", "c", "b" });
        var set = await col.ToHashSetAsync();
        Assert.Equal(3, set.Count);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task OrderByAsync_ReturnsSortedAscending()
    {
        var col = await CreateCollection("orderby_col", new[] { "cherry", "apple", "banana" });

        var ordered = new List<string>();
        await foreach (var item in col.OrderByAsync(s => s))
            ordered.Add(item);

        Assert.Equal(new[] { "apple", "banana", "cherry" }, ordered);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task OrderByDescendingAsync_ReturnsSortedDescending()
    {
        var col = await CreateCollection("orderbydesc_col", new[] { "cherry", "apple", "banana" });

        var ordered = new List<string>();
        await foreach (var item in col.OrderByDescendingAsync(s => s))
            ordered.Add(item);

        Assert.Equal(new[] { "cherry", "banana", "apple" }, ordered);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task AnyAsync_TrueWhenMatchExists()
    {
        var col = await CreateCollection("any_col", new[] { "hello", "world" });
        var any = await col.AnyAsync(s => s.StartsWith("h"));
        Assert.True(any);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task AnyAsync_FalseWhenNoMatch()
    {
        var col = await CreateCollection("any_false_col", new[] { "hello", "world" });
        var any = await col.AnyAsync(s => s.StartsWith("z"));
        Assert.False(any);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task AllAsync_TrueWhenAllMatch()
    {
        var col = await CreateCollection("all_col", new[] { "aardvark", "ant", "ape" });
        var all = await col.AllAsync(s => s.StartsWith("a"));
        Assert.True(all);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task AllAsync_FalseWhenOneDoesNotMatch()
    {
        var col = await CreateCollection("all_false_col", new[] { "ant", "bee", "cat" });
        var all = await col.AllAsync(s => s.StartsWith("a"));
        Assert.False(all);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task CountWhereAsync_ReturnsCorrectCount()
    {
        var col = await CreateCollection("countwhere_col", new[] { "ant", "apple", "bat", "bear", "ape" });
        var count = await col.CountWhereAsync(s => s.StartsWith("a"));
        Assert.Equal(3, count);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task MinAsync_ReturnsSmallestValue()
    {
        var scored = new[]
        {
            new ScoredItem("A", 50),
            new ScoredItem("B", 10),
            new ScoredItem("C", 80)
        };
        var col = await CreateCollection("min_col", scored);

        var min = await col.MinAsync(s => s.Score);
        Assert.Equal(10, min);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task MaxAsync_ReturnsLargestValue()
    {
        var scored = new[]
        {
            new ScoredItem("A", 50),
            new ScoredItem("B", 10),
            new ScoredItem("C", 80)
        };
        var col = await CreateCollection("max_col", scored);

        var max = await col.MaxAsync(s => s.Score);
        Assert.Equal(80, max);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task SumAsync_ReturnsCorrectSum()
    {
        var scored = new[]
        {
            new ScoredItem("A", 10),
            new ScoredItem("B", 20),
            new ScoredItem("C", 30)
        };
        var col = await CreateCollection("sum_col", scored);

        var sum = await col.SumAsync(s => (double)s.Score);
        Assert.Equal(60.0, sum);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task AverageAsync_ReturnsCorrectAverage()
    {
        var scored = new[]
        {
            new ScoredItem("A", 10),
            new ScoredItem("B", 20),
            new ScoredItem("C", 30)
        };
        var col = await CreateCollection("avg_col", scored);

        var avg = await col.AverageAsync(s => (double)s.Score);
        Assert.Equal(20.0, avg);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task GroupByAsync_GroupsCorrectly()
    {
        var scored = new[]
        {
            new ScoredItem("Alice", 10),
            new ScoredItem("Alice", 20),
            new ScoredItem("Bob", 30)
        };
        var col = await CreateCollection("groupby_col", scored);

        var groups = await col.GroupByAsync(s => s.Name);

        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups["Alice"].Count);
        Assert.Single(groups["Bob"]);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task PageAsync_ReturnsCorrectPage()
    {
        // 10 items, page size 3 — page 1 (0-indexed) should be items 3,4,5
        var col = await CreateCollection("page_col",
            Enumerable.Range(1, 10).Select(i => new ScoredItem($"Item{i}", i)));

        var page = new List<ScoredItem>();
        await foreach (var item in col.PageAsync(pageIndex: 1, pageSize: 3))
            page.Add(item);

        Assert.Equal(3, page.Count);
        Assert.Equal(4, page[0].Score);
        Assert.Equal(5, page[1].Score);
        Assert.Equal(6, page[2].Score);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task BatchAsync_ReturnsCorrectBatchSizes()
    {
        var col = await CreateCollection("batch_col",
            Enumerable.Range(1, 7).Select(i => new ScoredItem($"Item{i}", i)));

        var batches = new List<List<ScoredItem>>();
        await foreach (var batch in col.BatchAsync(3))
            batches.Add(batch);

        // 7 items in batches of 3: [3, 3, 1]
        Assert.Equal(3, batches.Count);
        Assert.Equal(3, batches[0].Count);
        Assert.Equal(3, batches[1].Count);
        Assert.Single(batches[2]);
        await col.DisposeAsync();
    }

    [Fact]
    public async Task DistinctAsync_Deduplicates()
    {
        var col = await CreateCollection("distinct_col",
            new[] { "apple", "banana", "apple", "cherry", "banana" });

        var distinct = new List<string>();
        await foreach (var item in col.DistinctAsync())
            distinct.Add(item);

        Assert.Equal(3, distinct.Count);
        Assert.Contains("apple", distinct);
        Assert.Contains("banana", distinct);
        Assert.Contains("cherry", distinct);
        await col.DisposeAsync();
    }
}
