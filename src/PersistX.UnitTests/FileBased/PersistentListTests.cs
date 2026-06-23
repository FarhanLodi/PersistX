using PersistX.FileBased;
using Xunit;

namespace PersistX.UnitTests.FileBased;

public class PersistentListTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public PersistentListTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "persistx_list_" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "test.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Add_SingleItem_CountIsOne()
    {
        var list = new PersistentList<string>(_path);
        await list.AddAsync("hello");
        var count = await list.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Add_MultipleItems_CountMatchesAdded()
    {
        var list = new PersistentList<int>(_path);
        await list.AddAsync(1);
        await list.AddAsync(2);
        await list.AddAsync(3);
        var count = await list.CountAsync();
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task AddRange_BatchAdd_CountMatchesTotal()
    {
        var list = new PersistentList<string>(_path);
        await list.AddRangeAsync(new[] { "a", "b", "c", "d", "e" });
        var count = await list.CountAsync();
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task GetAt_ValidIndex_ReturnsCorrectItem()
    {
        var list = new PersistentList<string>(_path);
        await list.AddRangeAsync(new[] { "first", "second", "third" });
        var item = await list.GetAtAsync(1);
        Assert.Equal("second", item);
    }

    [Fact]
    public async Task Remove_ExistingItem_ReturnsTrueAndDecrementsCount()
    {
        var list = new PersistentList<string>(_path);
        await list.AddAsync("alpha");
        await list.AddAsync("beta");
        var removed = await list.RemoveAsync("alpha");
        var count = await list.CountAsync();
        Assert.True(removed);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Remove_NonExistentItem_ReturnsFalse()
    {
        var list = new PersistentList<string>(_path);
        await list.AddAsync("alpha");
        var removed = await list.RemoveAsync("gamma");
        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveAt_ValidIndex_RemovesItem()
    {
        var list = new PersistentList<string>(_path);
        await list.AddRangeAsync(new[] { "a", "b", "c" });
        await list.RemoveAtAsync(1); // remove "b"
        var count = await list.CountAsync();
        var item0 = await list.GetAtAsync(0);
        var item1 = await list.GetAtAsync(1);
        Assert.Equal(2, count);
        Assert.Equal("a", item0);
        Assert.Equal("c", item1);
    }

    [Fact]
    public async Task Clear_RemovesAllItems()
    {
        var list = new PersistentList<int>(_path);
        await list.AddRangeAsync(new[] { 1, 2, 3, 4, 5 });
        await list.ClearAsync();
        var count = await list.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Contains_ExistingItem_ReturnsTrue()
    {
        var list = new PersistentList<string>(_path);
        await list.AddAsync("needle");
        var contains = await list.ContainsAsync("needle");
        Assert.True(contains);
    }

    [Fact]
    public async Task Contains_MissingItem_ReturnsFalse()
    {
        var list = new PersistentList<string>(_path);
        await list.AddAsync("hay");
        var contains = await list.ContainsAsync("needle");
        Assert.False(contains);
    }

    [Fact]
    public async Task GetAll_ReturnsAllItems()
    {
        var list = new PersistentList<string>(_path);
        var expected = new[] { "x", "y", "z" };
        await list.AddRangeAsync(expected);

        var all = new List<string>();
        await foreach (var item in list.GetAllAsync())
            all.Add(item);

        Assert.Equal(expected, all);
    }

    [Fact]
    public async Task GetRange_ReturnsCorrectSubset()
    {
        var list = new PersistentList<int>(_path);
        await list.AddRangeAsync(new[] { 10, 20, 30, 40, 50 });
        var range = await list.GetRangeAsync(1, 3);
        Assert.Equal(new[] { 20, 30, 40 }, range);
    }

    [Fact]
    public async Task Sort_OrdersItemsAscending()
    {
        var list = new PersistentList<int>(_path);
        await list.AddRangeAsync(new[] { 5, 3, 1, 4, 2 });
        await list.SortAsync();

        var all = new List<int>();
        await foreach (var item in list.GetAllAsync())
            all.Add(item);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, all);
    }

    [Fact]
    public async Task Persistence_DataSurvivesNewInstance()
    {
        // Write data with first instance
        var list1 = new PersistentList<string>(_path);
        await list1.AddRangeAsync(new[] { "persist1", "persist2", "persist3" });

        // Create a brand-new instance pointing to the same file
        var list2 = new PersistentList<string>(_path);
        var count = await list2.CountAsync();
        var item0 = await list2.GetAtAsync(0);
        var item2 = await list2.GetAtAsync(2);

        Assert.Equal(3, count);
        Assert.Equal("persist1", item0);
        Assert.Equal("persist3", item2);
    }

    [Fact]
    public async Task ConcurrentAdds_ThreadSafe()
    {
        var list = new PersistentList<int>(_path);
        var tasks = Enumerable.Range(0, 10)
            .Select(i => Task.Run(async () =>
            {
                for (int j = 0; j < 10; j++)
                    await list.AddAsync(i * 10 + j);
            }));

        await Task.WhenAll(tasks);

        var count = await list.CountAsync();
        Assert.Equal(100, count);
    }

    [Fact]
    public async Task IndexOf_ExistingItem_ReturnsCorrectIndex()
    {
        var list = new PersistentList<string>(_path);
        await list.AddRangeAsync(new[] { "apple", "banana", "cherry" });
        var index = await list.IndexOfAsync("banana");
        Assert.Equal(1, index);
    }

    [Fact]
    public async Task SetAt_UpdatesItem()
    {
        var list = new PersistentList<string>(_path);
        await list.AddRangeAsync(new[] { "old", "middle", "end" });
        await list.SetAtAsync(0, "new");
        var item = await list.GetAtAsync(0);
        Assert.Equal("new", item);
    }

    [Fact]
    public async Task Insert_AtIndex_ShiftsItems()
    {
        var list = new PersistentList<string>(_path);
        await list.AddRangeAsync(new[] { "a", "c" });
        await list.InsertAsync(1, "b");

        var count = await list.CountAsync();
        var item1 = await list.GetAtAsync(1);
        var item2 = await list.GetAtAsync(2);

        Assert.Equal(3, count);
        Assert.Equal("b", item1);
        Assert.Equal("c", item2);
    }
}
