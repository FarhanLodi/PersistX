using System.Linq.Expressions;
using PersistX.Indexes;
using PersistX.Storage;
using PersistX.UnitTests;
using Xunit;

namespace PersistX.UnitTests.Indexes;

public record EmployeeRecord(string Id, string Department, int Salary);

public class PersistentHashIndexTests : IAsyncLifetime
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

    private PersistentHashIndex<string, EmployeeRecord> CreateIndex(string name = "emp_idx")
    {
        Expression<Func<EmployeeRecord, string>> keySelector = e => e.Id;
        return new PersistentHashIndex<string, EmployeeRecord>(name, keySelector, _storage);
    }

    [Fact]
    public async Task AddItems_ThenFindThem()
    {
        var index = CreateIndex();
        await index.InitializeAsync();

        var emp = new EmployeeRecord("E001", "Engineering", 100_000);
        await index.AddAsync("E001", emp);

        var results = new List<EmployeeRecord>();
        await foreach (var item in index.FindAsync("E001"))
            results.Add(item);

        Assert.Single(results);
        Assert.Equal("Engineering", results[0].Department);
    }

    [Fact]
    public async Task AddMultipleItems_FindEach()
    {
        var index = CreateIndex();
        await index.InitializeAsync();

        var employees = new[]
        {
            new EmployeeRecord("E001", "Engineering", 100_000),
            new EmployeeRecord("E002", "Marketing", 80_000),
            new EmployeeRecord("E003", "Sales", 70_000)
        };

        foreach (var e in employees)
            await index.AddAsync(e.Id, e);

        foreach (var e in employees)
        {
            var found = new List<EmployeeRecord>();
            await foreach (var item in index.FindAsync(e.Id))
                found.Add(item);

            Assert.Single(found);
            Assert.Equal(e.Department, found[0].Department);
        }
    }

    [Fact]
    public async Task AfterRebuildAsync_AllItemsFindable()
    {
        var index = CreateIndex();
        await index.InitializeAsync();

        var employees = new[]
        {
            new EmployeeRecord("E001", "Engineering", 100_000),
            new EmployeeRecord("E002", "Marketing", 80_000),
        };

        await index.RebuildAsync(employees.ToAsyncEnumerable());

        foreach (var e in employees)
        {
            var found = new List<EmployeeRecord>();
            await foreach (var item in index.FindAsync(e.Id))
                found.Add(item);

            Assert.Single(found);
        }
    }

    [Fact]
    public async Task ClearAsync_EmptiesIndex()
    {
        var index = CreateIndex();
        await index.InitializeAsync();

        await index.AddAsync("E001", new EmployeeRecord("E001", "Engineering", 100_000));
        await index.AddAsync("E002", new EmployeeRecord("E002", "Marketing", 80_000));

        await index.ClearAsync();

        var count = await index.GetCountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Persistence_AfterAddItems_NewInstanceFindsItems()
    {
        // Add items using first index instance
        var index1 = CreateIndex("persistent_emp");
        await index1.InitializeAsync();

        await index1.AddAsync("E001", new EmployeeRecord("E001", "Engineering", 100_000));
        await index1.AddAsync("E002", new EmployeeRecord("E002", "Marketing", 80_000));
        await index1.DisposeAsync();

        // Create a new index instance pointing to the same backend/name
        var index2 = CreateIndex("persistent_emp");
        await index2.InitializeAsync(); // should load from backend

        Assert.True(await index2.ContainsKeyAsync("E001"));
        Assert.True(await index2.ContainsKeyAsync("E002"));
        await index2.DisposeAsync();
    }

    [Fact]
    public async Task ContainsKeyAsync_ReturnsTrueForExistingKey()
    {
        var index = CreateIndex();
        await index.InitializeAsync();
        await index.AddAsync("E001", new EmployeeRecord("E001", "HR", 60_000));

        Assert.True(await index.ContainsKeyAsync("E001"));
    }

    [Fact]
    public async Task ContainsKeyAsync_ReturnsFalseForMissingKey()
    {
        var index = CreateIndex();
        await index.InitializeAsync();

        Assert.False(await index.ContainsKeyAsync("MISSING"));
    }

    [Fact]
    public async Task GetAllKeysAsync_ReturnsAllKeys()
    {
        var index = CreateIndex();
        await index.InitializeAsync();

        await index.AddAsync("E001", new EmployeeRecord("E001", "Engineering", 100_000));
        await index.AddAsync("E002", new EmployeeRecord("E002", "Marketing", 80_000));
        await index.AddAsync("E003", new EmployeeRecord("E003", "Sales", 70_000));

        var keys = new List<string>();
        await foreach (var key in index.GetAllKeysAsync())
            keys.Add(key);

        Assert.Equal(3, keys.Count);
        Assert.Contains("E001", keys);
        Assert.Contains("E002", keys);
        Assert.Contains("E003", keys);
    }
}
