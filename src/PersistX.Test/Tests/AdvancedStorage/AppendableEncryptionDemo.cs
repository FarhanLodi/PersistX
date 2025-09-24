using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PersistX.Storage;
using PersistX.Encryption;
using PersistX.Database;
using Microsoft.Extensions.Logging;

namespace PersistX.Test.Tests.AdvancedStorage;

/// <summary>
/// Demo to show encryption functionality (now always appendable).
/// </summary>
public class AppendableEncryptionDemo
{
    private static string GetDataPath(string fileName)
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "persistx_data");
        Directory.CreateDirectory(dataDir);
        var fullPath = Path.Combine(dataDir, fileName);
        Console.WriteLine($"📁 Database will be saved to: {Path.GetFullPath(fullPath)}");
        return fullPath;
    }

    public static async Task RunAppendableDemoAsync()
    {
        Console.WriteLine("=== Encryption Demo (Always Appendable) ===");
        
        // Clean up any existing test files
        var testDbPath = GetDataPath("appendable_demo.db");
        if (Directory.Exists(testDbPath))
        {
            Directory.Delete(testDbPath, true);
        }

        try
        {
            // Generate a consistent encryption key
            var encryptionKey = await AesEncryptionProvider.GenerateKeyBase64Async();
            Console.WriteLine($"Using encryption key: {encryptionKey.Substring(0, 20)}...");

            // Test 1: First write and read - should work
            Console.WriteLine("\n--- Test 1: First write and read ---");
            await TestAppendableWriteAndRead(encryptionKey, testDbPath, "Test 1", new[] { "item1", "item2", "item3" });

            // Test 2: Append more data to the same file - should work
            Console.WriteLine("\n--- Test 2: Append more data to same file ---");
            await TestAppendableWriteAndRead(encryptionKey, testDbPath, "Test 2", new[] { "item4", "item5", "item6" });

            // Test 3: Append different length data - should work
            Console.WriteLine("\n--- Test 3: Append different length data ---");
            await TestAppendableWriteAndRead(encryptionKey, testDbPath, "Test 3", new[] { "short" });

            // Test 4: Append longer data - should work
            Console.WriteLine("\n--- Test 4: Append longer data ---");
            await TestAppendableWriteAndRead(encryptionKey, testDbPath, "Test 4", new[] { "very_long_item_name_1", "very_long_item_name_2" });

            Console.WriteLine("\n✅ All encryption tests completed successfully!");
            Console.WriteLine("The system now always supports appending encrypted data to existing files!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Test failed! Error: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static async Task TestAppendableWriteAndRead(string encryptionKey, string dbPath, string testName, string[] itemsToAdd)
    {
        // Create database with encryption (now always appendable)
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = dbPath,
                ["EncryptionType"] = "Aes",
                ["EncryptionKey"] = encryptionKey
            }
        };

        // Create logger factory for detailed logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        var database = new PersistX.Database.Database($"AppendableDemo_{testName}", new FileStorage(loggerFactory.CreateLogger<FileStorage>()), config, loggerFactory.CreateLogger<PersistX.Database.Database>());
        await database.InitializeAsync();

        // Get or create collection
        var collection = await database.GetCollectionAsync<string>("test_items") 
                        ?? await database.CreateCollectionAsync<string>("test_items");

        // Add items
        foreach (var item in itemsToAdd)
        {
            await collection.AddAsync(item);
        }

        Console.WriteLine($"{testName}: Added {itemsToAdd.Length} items to collection");

        // Read back items
        var retrievedItems = new List<string>();
        await foreach (var item in collection.GetAllAsync())
        {
            retrievedItems.Add(item);
        }

        Console.WriteLine($"{testName}: Retrieved {retrievedItems.Count} items from collection");
        
        if (retrievedItems.Count == 0)
        {
            throw new InvalidOperationException($"{testName}: No items retrieved - appendable encryption/decryption failed!");
        }

        foreach (var item in retrievedItems)
        {
            Console.WriteLine($"  - {item}");
        }

        // Dispose database
        await database.DisposeAsync();
    }
}
