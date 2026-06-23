using System.Linq.Expressions;
using PersistX.Indexes;
using PersistX.Interfaces;
using Xunit;

namespace PersistX.UnitTests.Indexes;

public record PersonRecord(string Name, int Age);

public class HashIndexTests
{
    private static HashIndex<string, PersonRecord> CreateIndex(string name = "test_idx")
    {
        Expression<Func<PersonRecord, string>> keySelector = p => p.Name;
        return new HashIndex<string, PersonRecord>(name, keySelector, null);
    }

    [Fact]
    public async Task AddAsync_ThenFindAsync_ReturnsItem()
    {
        var index = CreateIndex();
        var person = new PersonRecord("Alice", 30);
        await index.AddAsync("Alice", person);

        var results = new List<PersonRecord>();
        await foreach (var item in index.FindAsync("Alice"))
            results.Add(item);

        Assert.Single(results);
        Assert.Equal("Alice", results[0].Name);
    }

    [Fact]
    public async Task AddAsync_DuplicateKeys_FindAsyncReturnsBoth()
    {
        var index = CreateIndex();
        var p1 = new PersonRecord("Bob", 25);
        var p2 = new PersonRecord("Bob", 35); // same key, different value
        await index.AddAsync("Bob", p1);
        await index.AddAsync("Bob", p2);

        var results = new List<PersonRecord>();
        await foreach (var item in index.FindAsync("Bob"))
            results.Add(item);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task RemoveAsync_RemovesCorrectItem()
    {
        var index = CreateIndex();
        var alice = new PersonRecord("Alice", 30);
        var bob = new PersonRecord("Bob", 25);
        await index.AddAsync("Alice", alice);
        await index.AddAsync("Bob", bob);

        await index.RemoveAsync("Alice", alice);

        Assert.False(await index.ContainsKeyAsync("Alice"));
        Assert.True(await index.ContainsKeyAsync("Bob"));
    }

    [Fact]
    public async Task ContainsKeyAsync_ExistingKey_ReturnsTrue()
    {
        var index = CreateIndex();
        await index.AddAsync("Charlie", new PersonRecord("Charlie", 40));
        Assert.True(await index.ContainsKeyAsync("Charlie"));
    }

    [Fact]
    public async Task ContainsKeyAsync_MissingKey_ReturnsFalse()
    {
        var index = CreateIndex();
        Assert.False(await index.ContainsKeyAsync("Nobody"));
    }

    [Fact]
    public async Task GetAllKeysAsync_ReturnsAllUniqueKeys()
    {
        var index = CreateIndex();
        await index.AddAsync("Alice", new PersonRecord("Alice", 30));
        await index.AddAsync("Bob", new PersonRecord("Bob", 25));
        await index.AddAsync("Alice", new PersonRecord("Alice", 31)); // duplicate key

        var keys = new List<string>();
        await foreach (var key in index.GetAllKeysAsync())
            keys.Add(key);

        // Should have 2 unique keys
        Assert.Equal(2, keys.Distinct().Count());
        Assert.Contains("Alice", keys);
        Assert.Contains("Bob", keys);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsTotalValues()
    {
        var index = CreateIndex();
        await index.AddAsync("Alice", new PersonRecord("Alice", 30));
        await index.AddAsync("Bob", new PersonRecord("Bob", 25));
        await index.AddAsync("Alice", new PersonRecord("Alice", 31));

        var count = await index.GetCountAsync();
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task ClearAsync_EmptiesIndex()
    {
        var index = CreateIndex();
        await index.AddAsync("Alice", new PersonRecord("Alice", 30));
        await index.AddAsync("Bob", new PersonRecord("Bob", 25));

        await index.ClearAsync();

        var count = await index.GetCountAsync();
        Assert.Equal(0, count);
        Assert.False(await index.ContainsKeyAsync("Alice"));
    }

    [Fact]
    public async Task UpdateAsync_ChangesKey()
    {
        var index = CreateIndex();
        var person = new PersonRecord("Alice", 30);
        await index.AddAsync("Alice", person);

        await index.UpdateAsync("Alice", "Alicia", person);

        Assert.False(await index.ContainsKeyAsync("Alice"));
        Assert.True(await index.ContainsKeyAsync("Alicia"));
    }

    [Fact]
    public async Task RebuildAsync_RebuildsFromStream()
    {
        var index = CreateIndex();
        // Pre-populate with some data
        await index.AddAsync("Old", new PersonRecord("Old", 99));

        // Rebuild from a new stream
        var newData = new[] {
            new PersonRecord("New1", 10),
            new PersonRecord("New2", 20)
        };

        await index.RebuildAsync(newData.ToAsyncEnumerable());

        // Old key should be gone, new keys via keySelector (p.Name)
        var count = await index.GetCountAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task FindRangeAsync_ReturnsItemsInRange()
    {
        // HashIndex FindRangeAsync is a linear scan filtered by key comparison
        Expression<Func<PersonRecord, int>> ageSelector = p => p.Age;
        var index = new HashIndex<int, PersonRecord>("age_idx", ageSelector, null);

        await index.AddAsync(25, new PersonRecord("Alice", 25));
        await index.AddAsync(30, new PersonRecord("Bob", 30));
        await index.AddAsync(35, new PersonRecord("Charlie", 35));
        await index.AddAsync(40, new PersonRecord("Dave", 40));

        var results = new List<PersonRecord>();
        await foreach (var item in index.FindRangeAsync(28, 37))
            results.Add(item);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.InRange(r.Age, 28, 37));
    }
}

// Helper to convert IEnumerable to IAsyncEnumerable
internal static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
