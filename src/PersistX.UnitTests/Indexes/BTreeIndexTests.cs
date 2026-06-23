using System.Linq.Expressions;
using PersistX.Indexes;
using Xunit;

namespace PersistX.UnitTests.Indexes;

public record ProductRecord(int Id, string Name, double Price);

public class BTreeIndexTests
{
    private static BTreeIndex<int, ProductRecord> CreateIntIndex(string name = "btree_id")
    {
        Expression<Func<ProductRecord, int>> keySelector = p => p.Id;
        return new BTreeIndex<int, ProductRecord>(name, keySelector);
    }

    [Fact]
    public async Task Add_Find_SingleItem()
    {
        var index = CreateIntIndex();
        var product = new ProductRecord(1, "Widget", 9.99);
        await index.AddAsync(1, product);

        var results = new List<ProductRecord>();
        await foreach (var item in index.FindAsync(1))
            results.Add(item);

        Assert.Single(results);
        Assert.Equal("Widget", results[0].Name);
    }

    [Fact]
    public async Task Add_ManyItems_FindEach()
    {
        var index = CreateIntIndex();
        var products = Enumerable.Range(1, 100)
            .Select(i => new ProductRecord(i, $"Product{i}", i * 1.5))
            .ToList();

        foreach (var p in products)
            await index.AddAsync(p.Id, p);

        // Verify each can be found
        foreach (var p in products)
        {
            var found = new List<ProductRecord>();
            await foreach (var item in index.FindAsync(p.Id))
                found.Add(item);

            Assert.Single(found);
            Assert.Equal(p.Name, found[0].Name);
        }
    }

    [Fact]
    public async Task FindRangeAsync_ReturnsItemsInRange()
    {
        var index = CreateIntIndex();
        for (int i = 1; i <= 20; i++)
            await index.AddAsync(i, new ProductRecord(i, $"P{i}", i * 1.0));

        var results = new List<ProductRecord>();
        await foreach (var item in index.FindRangeAsync(5, 10))
            results.Add(item);

        Assert.Equal(6, results.Count); // 5,6,7,8,9,10
        Assert.All(results, r => Assert.InRange(r.Id, 5, 10));
    }

    [Fact]
    public async Task FindRangeAsync_ReturnsSortedOrder()
    {
        var index = CreateIntIndex();
        // Insert out of order
        var ids = new[] { 15, 3, 8, 1, 12, 6 };
        foreach (var id in ids)
            await index.AddAsync(id, new ProductRecord(id, $"P{id}", id * 1.0));

        var results = new List<ProductRecord>();
        await foreach (var item in index.FindRangeAsync(1, 15))
            results.Add(item);

        // BTree returns in sorted key order
        var resultIds = results.Select(r => r.Id).ToList();
        for (int i = 0; i < resultIds.Count - 1; i++)
            Assert.True(resultIds[i] <= resultIds[i + 1], $"Expected sorted but got {resultIds[i]} before {resultIds[i + 1]}");
    }

    [Fact]
    public async Task RemoveAsync_RemovesCorrectItem()
    {
        var index = CreateIntIndex();
        var p1 = new ProductRecord(1, "Alpha", 1.0);
        var p2 = new ProductRecord(2, "Beta", 2.0);
        await index.AddAsync(1, p1);
        await index.AddAsync(2, p2);

        await index.RemoveAsync(1, p1);

        Assert.False(await index.ContainsKeyAsync(1));
        Assert.True(await index.ContainsKeyAsync(2));
    }

    [Fact]
    public async Task GetAllKeysAsync_ReturnsAllKeysSorted()
    {
        var index = CreateIntIndex();
        var shuffled = new[] { 7, 2, 9, 1, 5, 3 };
        foreach (var id in shuffled)
            await index.AddAsync(id, new ProductRecord(id, $"P{id}", id * 1.0));

        var keys = new List<int>();
        await foreach (var key in index.GetAllKeysAsync())
            keys.Add(key);

        Assert.Equal(keys.OrderBy(k => k).ToList(), keys);
        Assert.Equal(shuffled.Length, keys.Count);
    }

    [Fact]
    public async Task LargeInsert_AndRangeQuery()
    {
        var index = CreateIntIndex();
        for (int i = 1; i <= 1000; i++)
            await index.AddAsync(i, new ProductRecord(i, $"Product{i}", i * 0.5));

        var results = new List<ProductRecord>();
        await foreach (var item in index.FindRangeAsync(100, 200))
            results.Add(item);

        Assert.Equal(101, results.Count); // 100..200 inclusive
        Assert.All(results, r => Assert.InRange(r.Id, 100, 200));
    }

    [Fact]
    public async Task DuplicateKeys_Supported()
    {
        Expression<Func<ProductRecord, double>> priceSelector = p => p.Price;
        var index = new BTreeIndex<double, ProductRecord>("btree_price", priceSelector);

        var p1 = new ProductRecord(1, "Cheap1", 9.99);
        var p2 = new ProductRecord(2, "Cheap2", 9.99); // same price
        await index.AddAsync(9.99, p1);
        await index.AddAsync(9.99, p2);

        var results = new List<ProductRecord>();
        await foreach (var item in index.FindAsync(9.99))
            results.Add(item);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        var index = CreateIntIndex();
        for (int i = 1; i <= 50; i++)
            await index.AddAsync(i, new ProductRecord(i, $"P{i}", i * 1.0));

        var count = await index.GetCountAsync();
        Assert.Equal(50, count);
    }

    [Fact]
    public async Task ClearAsync_EmptiesIndex()
    {
        var index = CreateIntIndex();
        for (int i = 1; i <= 10; i++)
            await index.AddAsync(i, new ProductRecord(i, $"P{i}", i * 1.0));

        await index.ClearAsync();

        var count = await index.GetCountAsync();
        Assert.Equal(0, count);
    }
}
