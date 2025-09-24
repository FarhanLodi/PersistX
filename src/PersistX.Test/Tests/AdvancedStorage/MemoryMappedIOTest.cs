using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PersistX.Storage;
using PersistX.Database;
using Microsoft.Extensions.Logging;

namespace PersistX.Test.Tests.AdvancedStorage;

/// <summary>
/// Test for Memory-Mapped I/O functionality.
/// </summary>
public class MemoryMappedIOTest
{
    private static string GetDataPath(string fileName)
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "persistx_data");
        Directory.CreateDirectory(dataDir);
        var fullPath = Path.Combine(dataDir, fileName);
        Console.WriteLine($"📁 Database will be saved to: {Path.GetFullPath(fullPath)}");
        return fullPath;
    }

    public static async Task RunMemoryMappedIOTestAsync()
    {
        Console.WriteLine("=== Memory-Mapped I/O Test ===");
        
        // Clean up any existing test files
        var testDbPath = GetDataPath("mmf_test.db");
        if (Directory.Exists(testDbPath))
        {
            Directory.Delete(testDbPath, true);
        }

        try
        {
            // Test 1: Memory-mapped I/O enabled
            Console.WriteLine("\n--- Test 1: Memory-Mapped I/O Enabled ---");
            var testDbPath1 = GetDataPath("mmf_test_1.db");
            await TestMemoryMappedIO(testDbPath1, true, "mmf_enabled");

            // Test 2: Memory-mapped I/O disabled (baseline)
            Console.WriteLine("\n--- Test 2: Memory-Mapped I/O Disabled (Baseline) ---");
            var testDbPath2 = GetDataPath("mmf_test_2.db");
            await TestMemoryMappedIO(testDbPath2, false, "mmf_disabled");

            // Test 3: Large data with memory-mapped I/O
            Console.WriteLine("\n--- Test 3: Large Data with Memory-Mapped I/O ---");
            var testDbPath3 = GetDataPath("mmf_test_3.db");
            await TestLargeDataWithMMF(testDbPath3);

            Console.WriteLine("\n✅ All memory-mapped I/O tests completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Memory-mapped I/O test failed! Error: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            // Clean up test files
            try
            {
                if (Directory.Exists(testDbPath))
                {
                    Directory.Delete(testDbPath, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static async Task TestMemoryMappedIO(string dbPath, bool enableMMF, string testName)
    {
        // Create database with memory-mapped I/O setting
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = dbPath,
                ["EnableMemoryMappedIO"] = enableMMF.ToString().ToLowerInvariant()
            }
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        var database = new PersistX.Database.Database($"MMFTest_{testName}", new FileStorage(loggerFactory.CreateLogger<FileStorage>()), config, loggerFactory.CreateLogger<PersistX.Database.Database>());
        await database.InitializeAsync();

        // Create collection and add data
        var collection = await database.CreateCollectionAsync<string>($"{testName}_items");

        var testItems = new[]
        {
            "This is a test item for memory-mapped I/O testing",
            "Another test item to verify memory-mapped file operations",
            "Yet another test item to ensure data integrity with MMF",
            "Final test item to complete the memory-mapped I/O test suite"
        };

        foreach (var item in testItems)
        {
            await collection.AddAsync(item);
        }

        Console.WriteLine($"Added {testItems.Length} items with MMF {(enableMMF ? "enabled" : "disabled")}");

        // Verify data was written and can be read back
        var retrievedItems = new List<string>();
        await foreach (var item in collection.GetAllAsync())
        {
            retrievedItems.Add(item);
        }

        Console.WriteLine($"Retrieved {retrievedItems.Count} items with MMF {(enableMMF ? "enabled" : "disabled")}");
        
        if (retrievedItems.Count != testItems.Length)
        {
            throw new InvalidOperationException($"Expected {testItems.Length} items, but got {retrievedItems.Count}");
        }

        // Verify data integrity
        for (int i = 0; i < testItems.Length; i++)
        {
            if (retrievedItems[i] != testItems[i])
            {
                throw new InvalidOperationException($"Data integrity check failed for item {i}");
            }
        }

        Console.WriteLine($"Data integrity verified for MMF {(enableMMF ? "enabled" : "disabled")} test");

        await database.DisposeAsync();
    }

    private static async Task TestLargeDataWithMMF(string dbPath)
    {
        // Create database with memory-mapped I/O enabled
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = dbPath,
                ["EnableMemoryMappedIO"] = "true"
            }
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        var database = new PersistX.Database.Database("LargeDataMMFTest", new FileStorage(loggerFactory.CreateLogger<FileStorage>()), config, loggerFactory.CreateLogger<PersistX.Database.Database>());
        await database.InitializeAsync();

        var collection = await database.CreateCollectionAsync<string>("large_data_mmf_items");

        // Create large text data that will benefit from memory-mapped I/O
        var largeTextData = new List<string>();
        for (int i = 0; i < 50; i++)
        {
            var largeText = $"Large text item {i} designed for memory-mapped I/O testing. " +
                           $"This text is intentionally long to test the efficiency of memory-mapped file operations. " +
                           $"Memory-mapped I/O should provide better performance for large data reads. " +
                           $"The operating system handles the mapping between virtual memory and physical files. " +
                           $"This reduces the overhead of system calls and buffer copying. " +
                           $"Item {i} of 50 items designed to test memory-mapped I/O performance. " +
                           $"Each item contains substantial text to ensure the memory-mapped I/O threshold is exceeded. " +
                           $"This should trigger the memory-mapped file path in the FileStorage implementation.";
            
            largeTextData.Add(largeText);
        }

        Console.WriteLine($"Creating {largeTextData.Count} large text items for memory-mapped I/O testing...");

        // Add all items
        foreach (var item in largeTextData)
        {
            await collection.AddAsync(item);
        }

        Console.WriteLine($"Added {largeTextData.Count} large items with memory-mapped I/O enabled");

        // Verify data integrity
        var retrievedItems = new List<string>();
        await foreach (var item in collection.GetAllAsync())
        {
            retrievedItems.Add(item);
        }

        Console.WriteLine($"Retrieved {retrievedItems.Count} large items using memory-mapped I/O");
        
        if (retrievedItems.Count != largeTextData.Count)
        {
            throw new InvalidOperationException($"Expected {largeTextData.Count} items, but got {retrievedItems.Count}");
        }

        // Verify data integrity
        for (int i = 0; i < largeTextData.Count; i++)
        {
            if (retrievedItems[i] != largeTextData[i])
            {
                throw new InvalidOperationException($"Data integrity check failed for large item {i}");
            }
        }

        Console.WriteLine("Large data integrity verified with memory-mapped I/O");

        // Get statistics
        var stats = await database.GetStatisticsAsync();
        Console.WriteLine($"Total storage size: {stats.TotalStorageSize} bytes");

        await database.DisposeAsync();
    }
}
