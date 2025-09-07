using PersistX.FileBased;
using PersistX.Database;
using PersistX.Collections;
using System.Diagnostics;

namespace PersistX.Test.Tests;

/// <summary>
/// Performance tests to demonstrate the speed and efficiency of PersistX collections
/// </summary>
public class PerformanceTests
{
    private static string GetDataPath(string fileName)
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "persistx_data");
        Directory.CreateDirectory(dataDir);
        var fullPath = Path.Combine(dataDir, fileName);
        Console.WriteLine($"📁 Test data will be saved to: {Path.GetFullPath(fullPath)}");
        return fullPath;
    }
    public static async Task RunPerformanceTestsAsync()
    {
        Console.WriteLine("=== Performance Tests ===");
        Console.WriteLine("These tests measure the performance of different PersistX operations.");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("Choose a performance test:");
            Console.WriteLine("1) Simple Collections Performance - List, Dictionary, Set operations");
            Console.WriteLine("2) Database Collections Performance - CRUD operations with indexes");
            Console.WriteLine("3) Bulk Operations Performance - Large dataset operations");
            Console.WriteLine("4) Memory Usage Test - Memory consumption analysis");
            Console.WriteLine("5) All Performance Tests - Run all tests");
            Console.WriteLine("0) Back to main menu");
            Console.Write("Select an option: ");

            var input = Console.ReadLine();
            Console.WriteLine();

            switch (input)
            {
                case "1":
                    await SimpleCollectionsPerformanceTestAsync();
                    break;
                case "2":
                    await DatabaseCollectionsPerformanceTestAsync();
                    break;
                case "3":
                    await BulkOperationsPerformanceTestAsync();
                    break;
                case "4":
                    await MemoryUsageTestAsync();
                    break;
                case "5":
                    await AllPerformanceTestsAsync();
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Invalid selection. Try again.");
                    break;
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            Console.Clear();
        }
    }

    private static async Task SimpleCollectionsPerformanceTestAsync()
    {
        Console.WriteLine("=== Simple Collections Performance Test ===");
        Console.WriteLine("Testing the performance of simple persistent collections.");
        Console.WriteLine();

        // Test PersistentList
        Console.WriteLine("1. PersistentList Performance Test");
        var list = new PersistentList<string>(GetDataPath("perf_test_list.json"));
        
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            await list.AddAsync($"Item {i}");
        }
        stopwatch.Stop();
        
        Console.WriteLine($"   Added 1000 items in: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"   Average per item: {stopwatch.ElapsedMilliseconds / 1000.0:F2}ms");

        // Test PersistentDictionary
        Console.WriteLine();
        Console.WriteLine("2. PersistentDictionary Performance Test");
        var dict = new PersistentDictionary<string, int>(GetDataPath("perf_test_dict.json"));
        
        stopwatch.Restart();
        for (int i = 0; i < 1000; i++)
        {
            await dict.SetAsync($"Key{i}", i);
        }
        stopwatch.Stop();
        
        Console.WriteLine($"   Set 1000 key-value pairs in: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"   Average per operation: {stopwatch.ElapsedMilliseconds / 1000.0:F2}ms");

        // Test PersistentSet
        Console.WriteLine();
        Console.WriteLine("3. PersistentSet Performance Test");
        var set = new PersistentSet<string>(GetDataPath("perf_test_set.json"));
        
        stopwatch.Restart();
        for (int i = 0; i < 1000; i++)
        {
            await set.AddAsync($"UniqueItem{i}");
        }
        stopwatch.Stop();
        
        Console.WriteLine($"   Added 1000 unique items in: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"   Average per item: {stopwatch.ElapsedMilliseconds / 1000.0:F2}ms");

        Console.WriteLine();
        Console.WriteLine("✅ Simple Collections Performance Test completed!");
    }

    private static async Task DatabaseCollectionsPerformanceTestAsync()
    {
        Console.WriteLine("=== Database Collections Performance Test ===");
        Console.WriteLine("Testing the performance of database collections with indexes.");
        Console.WriteLine();

        var database = await DatabaseFactory.CreateFileDatabaseAsync("PerformanceTest", GetDataPath("perf_test.db"));
        
        try
        {
            Console.WriteLine("1. Creating collection with indexes...");
            var collection = await database.CreateCollectionAsync<TestItem>("test_items");
            await collection.CreateIndexAsync("category_index", item => item.Category);
            await collection.CreateIndexAsync("value_index", item => item.Value);

            Console.WriteLine("2. Adding items performance test...");
            var stopwatch = Stopwatch.StartNew();
            
            // Generate all items first
            var items = new List<TestItem>();
            for (int i = 0; i < 1000; i++)
            {
                items.Add(new TestItem
                {
                    Id = i,
                    Name = $"Item {i}",
                    Category = $"Category {i % 10}",
                    Value = i * 1.5,
                    Timestamp = DateTime.UtcNow.AddMinutes(-i)
                });
            }
            
            // Add all items in one batch operation
            await collection.AddRangeAsync(items);
            
            stopwatch.Stop();
            Console.WriteLine($"   Added 1000 items with indexes in: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"   Average per item: {stopwatch.ElapsedMilliseconds / 1000.0:F2}ms");

            Console.WriteLine();
            Console.WriteLine("3. Simulating index-based searches...");
            
            // Simulate category search
            stopwatch.Restart();
            var categoryIndex = await collection.GetIndexAsync<string>("category_index");
            if (categoryIndex != null)
            {
                await foreach (var item in categoryIndex.FindAsync("Category 1"))
                {
                    // Simulate processing the found item
                }
            }
            stopwatch.Stop();
            Console.WriteLine($"   Category index search: {stopwatch.ElapsedMilliseconds}ms");

            // Simulate value range search
            stopwatch.Restart();
            var valueIndex = await collection.GetIndexAsync<double>("value_index");
            if (valueIndex != null)
            {
                await foreach (var item in valueIndex.FindRangeAsync(100.0, 200.0))
                {
                    // Simulate processing the found item
                }
            }
            stopwatch.Stop();
            Console.WriteLine($"   Value index search: {stopwatch.ElapsedMilliseconds}ms");

            Console.WriteLine();
            Console.WriteLine("✅ Database Collections Performance Test completed!");
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task BulkOperationsPerformanceTestAsync()
    {
        Console.WriteLine("=== Bulk Operations Performance Test ===");
        Console.WriteLine("Testing performance with large datasets.");
        Console.WriteLine();

        // Test with different sizes
        var sizes = new[] { 1000, 3000 };
        
        foreach (var size in sizes)
        {
            Console.WriteLine($"Testing with {size:N0} items:");
            
            // Simple List bulk test
            var list = new PersistentList<string>(GetDataPath($"bulk_test_list_{size}.json"));
            var stopwatch = Stopwatch.StartNew();
            
            // Generate all items first
            var items = new List<string>();
            for (int i = 0; i < size; i++)
            {
                items.Add($"BulkItem {i}");
            }
            
            // Add all items in one batch operation
            await list.AddRangeAsync(items);
            
            stopwatch.Stop();
            Console.WriteLine($"   PersistentList: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.ElapsedMilliseconds / (double)size:F3}ms per item)");

            // Simple Dictionary bulk test
            var dict = new PersistentDictionary<string, int>(GetDataPath($"bulk_test_dict_{size}.json"));
            stopwatch.Restart();
            
            // Generate all key-value pairs first
            var keyValuePairs = new List<KeyValuePair<string, int>>();
            for (int i = 0; i < size; i++)
            {
                keyValuePairs.Add(new KeyValuePair<string, int>($"BulkKey{i}", i));
            }
            
            // Add all items in one batch operation
            await dict.AddRangeAsync(keyValuePairs);
            
            stopwatch.Stop();
            Console.WriteLine($"   PersistentDictionary: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.ElapsedMilliseconds / (double)size:F3}ms per item)");

            Console.WriteLine();
        }

        Console.WriteLine("✅ Bulk Operations Performance Test completed!");
    }

    private static async Task MemoryUsageTestAsync()
    {
        Console.WriteLine("=== Memory Usage Test ===");
        Console.WriteLine("Testing memory consumption of different collection types.");
        Console.WriteLine();

        // Get initial memory
        var initialMemory = GC.GetTotalMemory(true);
        Console.WriteLine($"Initial memory usage: {initialMemory / 1024 / 1024:F2} MB");

        // Test Simple Collections memory usage
        Console.WriteLine();
        Console.WriteLine("1. Simple Collections Memory Test");
        
        var list = new PersistentList<string>(GetDataPath("memory_test_list.json"));
        var dict = new PersistentDictionary<string, object>(GetDataPath("memory_test_dict.json"));
        var set = new PersistentSet<string>(GetDataPath("memory_test_set.json"));

        for (int i = 0; i < 1000; i++)
        {
            await list.AddAsync($"MemoryTestItem {i}");
            await dict.SetAsync($"MemoryTestKey{i}", $"MemoryTestValue{i}");
            await set.AddAsync($"MemoryTestUnique{i}");
        }

        var afterSimpleMemory = GC.GetTotalMemory(true);
        var simpleMemoryUsed = afterSimpleMemory - initialMemory;
        Console.WriteLine($"   Memory used by simple collections: {simpleMemoryUsed / 1024:F2} KB");

        // Test Database Collections memory usage
        Console.WriteLine();
        Console.WriteLine("2. Database Collections Memory Test");
        
        var database = await DatabaseFactory.CreateFileDatabaseAsync("MemoryTest", GetDataPath("memory_test.db"));
        
        try
        {
            var collection = await database.CreateCollectionAsync<TestItem>("memory_test_items");
            await collection.CreateIndexAsync("category_index", item => item.Category);

            // Generate all items first
            var memoryTestItems = new List<TestItem>();
            for (int i = 0; i < 1000; i++)
            {
                memoryTestItems.Add(new TestItem
                {
                    Id = i,
                    Name = $"MemoryTestItem {i}",
                    Category = $"Category {i % 10}",
                    Value = i * 1.5,
                    Timestamp = DateTime.UtcNow
                });
            }
            
            // Add all items in one batch operation
            await collection.AddRangeAsync(memoryTestItems);

            var afterDatabaseMemory = GC.GetTotalMemory(true);
            var databaseMemoryUsed = afterDatabaseMemory - afterSimpleMemory;
            Console.WriteLine($"   Memory used by database collections: {databaseMemoryUsed / 1024:F2} KB");

            var totalMemoryUsed = afterDatabaseMemory - initialMemory;
            Console.WriteLine($"   Total memory used: {totalMemoryUsed / 1024:F2} KB");
        }
        finally
        {
            await database.DisposeAsync();
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(true);
        var memoryAfterGC = finalMemory - initialMemory;
        Console.WriteLine($"   Memory after garbage collection: {memoryAfterGC / 1024:F2} KB");

        Console.WriteLine();
        Console.WriteLine("✅ Memory Usage Test completed!");
    }

    private static async Task AllPerformanceTestsAsync()
    {
        Console.WriteLine("=== All Performance Tests ===");
        Console.WriteLine("Running all performance tests in sequence...");
        Console.WriteLine();

        var totalStopwatch = Stopwatch.StartNew();

        await SimpleCollectionsPerformanceTestAsync();
        Console.WriteLine();
        Console.WriteLine("Press any key to continue to next test...");
        Console.ReadKey();
        Console.Clear();

        await DatabaseCollectionsPerformanceTestAsync();
        Console.WriteLine();
        Console.WriteLine("Press any key to continue to next test...");
        Console.ReadKey();
        Console.Clear();

        await BulkOperationsPerformanceTestAsync();
        Console.WriteLine();
        Console.WriteLine("Press any key to continue to next test...");
        Console.ReadKey();
        Console.Clear();

        await MemoryUsageTestAsync();

        totalStopwatch.Stop();
        Console.WriteLine();
        Console.WriteLine($"✅ All Performance Tests completed in {totalStopwatch.ElapsedMilliseconds}ms!");
    }
}

// Test data model
public class TestItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
}
