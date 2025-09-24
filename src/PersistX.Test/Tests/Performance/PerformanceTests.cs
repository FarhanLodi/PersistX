using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using PersistX.FileBased;
using PersistX.Database;
using PersistX.Collections;
using PersistX.Test.Utils;

namespace PersistX.Test.Tests.Performance;

/// <summary>
/// Performance tests to demonstrate the speed and efficiency of PersistX collections.
/// </summary>
public class PerformanceTests : TestBase
{
    public PerformanceTests() : base("Performance Tests") { }

    protected override async Task ExecuteTestAsync()
    {
        await TestFileBasedListPerformanceAsync();
        await TestFileBasedDictionaryPerformanceAsync();
        await TestDatabaseListPerformanceAsync();
        await TestDatabaseDictionaryPerformanceAsync();
        await TestLargeDatasetPerformanceAsync();
    }

    private async Task TestFileBasedListPerformanceAsync()
    {
        TestHelper.DisplaySectionHeader("File-Based List Performance Test");
        
        var testDataPath = TestHelper.GetTestDataPath("performance_list_test.json");
        var list = new PersistentList<string>(testDataPath);

        // Test adding items
        var addTime = await MeasureExecutionTimeAsync(async () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                await list.AddAsync($"Item {i}");
            }
        }, "Adding 1000 items to file-based list");

        // Test reading items
        var readTime = await MeasureExecutionTimeAsync(async () =>
        {
            var count = await list.CountAsync();
            var items = await list.GetRangeAsync(0, count);
            Console.WriteLine($"Retrieved {items.Count} items");
        }, "Reading all items from file-based list");

        // Test searching
        var searchTime = await MeasureExecutionTimeAsync(async () =>
        {
            var found = await list.ContainsAsync("Item 500");
            Console.WriteLine($"Found 'Item 500': {found}");
        }, "Searching for item in file-based list");

        Console.WriteLine($"\n📊 File-Based List Performance Summary:");
        Console.WriteLine($"   Add: {TestHelper.FormatTimeSpan(addTime)}");
        Console.WriteLine($"   Read: {TestHelper.FormatTimeSpan(readTime)}");
        Console.WriteLine($"   Search: {TestHelper.FormatTimeSpan(searchTime)}");

        // File-based collections don't need explicit disposal
        TestHelper.CleanupTestData("performance_list_test.json");
    }

    private async Task TestFileBasedDictionaryPerformanceAsync()
    {
        TestHelper.DisplaySectionHeader("File-Based Dictionary Performance Test");
        
        var testDataPath = TestHelper.GetTestDataPath("performance_dict_test.json");
        var dictionary = new PersistentDictionary<string, string>(testDataPath);

        // Test adding items
        var addTime = await MeasureExecutionTimeAsync(async () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                await dictionary.SetAsync($"Key{i}", $"Value{i}");
            }
        }, "Adding 1000 key-value pairs to file-based dictionary");

        // Test reading items
        var readTime = await MeasureExecutionTimeAsync(async () =>
        {
            var count = 0;
            await foreach (var item in dictionary.GetAllAsync())
            {
                count++;
            }
            Console.WriteLine($"Retrieved {count} key-value pairs");
        }, "Reading all items from file-based dictionary");

        // Test searching
        var searchTime = await MeasureExecutionTimeAsync(async () =>
        {
            var (found, value) = await dictionary.TryGetAsync("Key500");
            Console.WriteLine($"Found 'Key500': {found}, Value: {value}");
        }, "Searching for key in file-based dictionary");

        Console.WriteLine($"\n📊 File-Based Dictionary Performance Summary:");
        Console.WriteLine($"   Add: {TestHelper.FormatTimeSpan(addTime)}");
        Console.WriteLine($"   Read: {TestHelper.FormatTimeSpan(readTime)}");
        Console.WriteLine($"   Search: {TestHelper.FormatTimeSpan(searchTime)}");

        // File-based collections don't need explicit disposal
        TestHelper.CleanupTestData("performance_dict_test.json");
    }

    private async Task TestDatabaseListPerformanceAsync()
    {
        TestHelper.DisplaySectionHeader("Database List Performance Test");
        
        var testDataPath = TestHelper.GetTestDataPath("performance_db_list_test.db");
        var database = await CreateDatabaseAsync("PerformanceTest", testDataPath);
        var list = await database.CreateCollectionAsync<string>("performance_list");

        // Test adding items
        var addTime = await MeasureExecutionTimeAsync(async () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                await list.AddAsync($"Item {i}");
            }
        }, "Adding 1000 items to database list");

        // Test reading items
        var readTime = await MeasureExecutionTimeAsync(async () =>
        {
            var count = await list.CountAsync;
            // For database collections, we need to iterate through all items
            var itemCount = 0;
            await foreach (var item in list)
            {
                itemCount++;
            }
            Console.WriteLine($"Retrieved {itemCount} items");
        }, "Reading all items from database list");

        // Test searching
        var searchTime = await MeasureExecutionTimeAsync(async () =>
        {
            var found = await list.ContainsAsync("Item 500");
            Console.WriteLine($"Found 'Item 500': {found}");
        }, "Searching for item in database list");

        Console.WriteLine($"\n📊 Database List Performance Summary:");
        Console.WriteLine($"   Add: {TestHelper.FormatTimeSpan(addTime)}");
        Console.WriteLine($"   Read: {TestHelper.FormatTimeSpan(readTime)}");
        Console.WriteLine($"   Search: {TestHelper.FormatTimeSpan(searchTime)}");

        await database.DisposeAsync();
        TestHelper.CleanupTestData("performance_db_list_test.db");
    }

    private async Task TestDatabaseDictionaryPerformanceAsync()
    {
        TestHelper.DisplaySectionHeader("Database Dictionary Performance Test");
        
        var testDataPath = TestHelper.GetTestDataPath("performance_db_dict_test.db");
        var database = await CreateDatabaseAsync("PerformanceTest", testDataPath);
        var list = await database.CreateCollectionAsync<string>("performance_dict");

        // Test adding items
        var addTime = await MeasureExecutionTimeAsync(async () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                await list.AddAsync($"Key{i}:Value{i}");
            }
        }, "Adding 1000 key-value pairs to database collection");

        // Test reading items
        var readTime = await MeasureExecutionTimeAsync(async () =>
        {
            var count = await list.CountAsync;
            // For database collections, we need to iterate through all items
            var itemCount = 0;
            await foreach (var item in list)
            {
                itemCount++;
            }
            Console.WriteLine($"Retrieved {itemCount} key-value pairs");
        }, "Reading all items from database collection");

        // Test searching
        var searchTime = await MeasureExecutionTimeAsync(async () =>
        {
            var found = await list.ContainsAsync("Key500:Value500");
            Console.WriteLine($"Found 'Key500:Value500': {found}");
        }, "Searching for key in database collection");

        Console.WriteLine($"\n📊 Database Collection Performance Summary:");
        Console.WriteLine($"   Add: {TestHelper.FormatTimeSpan(addTime)}");
        Console.WriteLine($"   Read: {TestHelper.FormatTimeSpan(readTime)}");
        Console.WriteLine($"   Search: {TestHelper.FormatTimeSpan(searchTime)}");

        await database.DisposeAsync();
        TestHelper.CleanupTestData("performance_db_dict_test.db");
    }

    private async Task TestLargeDatasetPerformanceAsync()
    {
        TestHelper.DisplaySectionHeader("Large Dataset Performance Test");
        
        var testDataPath = TestHelper.GetTestDataPath("large_dataset_test.db");
        var database = await CreateAdvancedDatabaseAsync(
            "LargeDatasetTest", 
            testDataPath, 
            enableWal: true, 
            compressionType: "GZip",
            enableMemoryMappedIO: true);

        var list = await database.CreateCollectionAsync<string>("large_list");

        // Test with larger dataset
        var addTime = await MeasureExecutionTimeAsync(async () =>
        {
            for (int i = 0; i < 10000; i++)
            {
                await list.AddAsync($"Large Item {i} with some additional data to make it more realistic");
            }
        }, "Adding 10,000 items to database list with advanced features");

        // Test reading with pagination
        var readTime = await MeasureExecutionTimeAsync(async () =>
        {
            var count = await list.CountAsync;
            // For database collections, we need to iterate through all items
            var itemCount = 0;
            await foreach (var item in list)
            {
                itemCount++;
            }
            Console.WriteLine($"Retrieved {itemCount} items");
        }, "Reading all items from large dataset");

        // Test statistics
        var stats = await database.GetStatisticsAsync();
        Console.WriteLine($"\n📊 Database Statistics:");
        Console.WriteLine($"   Total Collections: {stats.CollectionCount}");
        Console.WriteLine($"   Storage Size: {TestHelper.FormatBytes(stats.TotalStorageSize)}");
        Console.WriteLine($"   Active Transactions: {stats.ActiveTransactionCount}");
        Console.WriteLine($"   Created At: {stats.CreatedAt:yyyy-MM-dd HH:mm:ss}");

        Console.WriteLine($"\n📊 Large Dataset Performance Summary:");
        Console.WriteLine($"   Add: {TestHelper.FormatTimeSpan(addTime)}");
        Console.WriteLine($"   Read: {TestHelper.FormatTimeSpan(readTime)}");

        await database.DisposeAsync();
        TestHelper.CleanupTestData("large_dataset_test.db");
    }
}
