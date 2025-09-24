using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PersistX.Storage;
using PersistX.Database;
using Microsoft.Extensions.Logging;

namespace PersistX.Test.Tests.AdvancedStorage;

/// <summary>
/// Test for Data Compression functionality (GZip and Deflate).
/// </summary>
public class CompressionTest
{
    private static string GetDataPath(string fileName)
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "persistx_data");
        Directory.CreateDirectory(dataDir);
        var fullPath = Path.Combine(dataDir, fileName);
        Console.WriteLine($"📁 Database will be saved to: {Path.GetFullPath(fullPath)}");
        return fullPath;
    }

    public static async Task RunCompressionTestAsync()
    {
        Console.WriteLine("=== Data Compression Test ===");
        
        // Clean up any existing test files
        var testDbPath = GetDataPath("compression_test.db");
        if (Directory.Exists(testDbPath))
        {
            Directory.Delete(testDbPath, true);
        }

        try
        {
            // Test 1: GZip compression
            Console.WriteLine("\n--- Test 1: GZip Compression ---");
            await TestCompression(testDbPath, "GZip", "gzip_test");

            // Test 2: Deflate compression
            Console.WriteLine("\n--- Test 2: Deflate Compression ---");
            await TestCompression(testDbPath, "Deflate", "deflate_test");

            // Test 3: No compression (baseline)
            Console.WriteLine("\n--- Test 3: No Compression (Baseline) ---");
            await TestCompression(testDbPath, "None", "no_compression_test");

            // Test 4: Compression with large data
            Console.WriteLine("\n--- Test 4: Compression with Large Data ---");
            await TestLargeDataCompression(testDbPath);

            Console.WriteLine("\n✅ All compression tests completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Compression test failed! Error: {ex.Message}");
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

    private static async Task TestCompression(string dbPath, string compressionType, string testName)
    {
        // Create database with specified compression
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = dbPath,
                ["CompressionType"] = compressionType
            }
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        var database = new PersistX.Database.Database($"CompressionTest_{testName}", new FileStorage(loggerFactory.CreateLogger<FileStorage>()), config, loggerFactory.CreateLogger<PersistX.Database.Database>());
        await database.InitializeAsync();

        // Create collection with text data (good for compression)
        var collection = await database.CreateCollectionAsync<string>($"{testName}_items");

        // Add items with repetitive content (good for compression)
        var testItems = new[]
        {
            "This is a test item with repetitive content that should compress well",
            "Another test item with similar repetitive content for compression testing",
            "Yet another test item with repetitive content to test compression ratios",
            "More repetitive content for compression testing purposes",
            "Final test item with repetitive content to verify compression works"
        };

        foreach (var item in testItems)
        {
            await collection.AddAsync(item);
        }

        Console.WriteLine($"Added {testItems.Length} items with {compressionType} compression");

        // Verify data was written and can be read back
        var retrievedItems = new List<string>();
        await foreach (var item in collection.GetAllAsync())
        {
            retrievedItems.Add(item);
        }

        Console.WriteLine($"Retrieved {retrievedItems.Count} items with {compressionType} compression");
        
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

        // Get compression statistics
        var stats = await database.GetComprehensiveStatisticsAsync();
        Console.WriteLine($"Compression enabled: {stats.HasCompression}");
        Console.WriteLine($"Compression type: {stats.CompressionType}");
        Console.WriteLine($"Total storage size: {stats.TotalStorageSize} bytes");

        await database.DisposeAsync();
    }

    private static async Task TestLargeDataCompression(string dbPath)
    {
        // Test with GZip compression for large data
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = dbPath,
                ["CompressionType"] = "GZip"
            }
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        var database = new PersistX.Database.Database("LargeDataCompressionTest", new FileStorage(loggerFactory.CreateLogger<FileStorage>()), config, loggerFactory.CreateLogger<PersistX.Database.Database>());
        await database.InitializeAsync();

        var collection = await database.CreateCollectionAsync<string>("large_data_items");

        // Create large text data with repetitive patterns
        var largeTextData = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            var largeText = $"This is a large text item number {i} with repetitive content that should compress very well. " +
                           $"The quick brown fox jumps over the lazy dog. " +
                           $"Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                           $"Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. " +
                           $"Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris. " +
                           $"Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore. " +
                           $"Eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident. " +
                           $"Sunt in culpa qui officia deserunt mollit anim id est laborum. " +
                           $"This text is repeated many times to test compression effectiveness. " +
                           $"The more repetitive the content, the better the compression ratio should be. " +
                           $"Compression algorithms work best with redundant and repetitive data patterns. " +
                           $"This is item {i} of 100 items designed to test large data compression.";
            
            largeTextData.Add(largeText);
        }

        Console.WriteLine($"Creating {largeTextData.Count} large text items for compression testing...");

        // Add all items
        foreach (var item in largeTextData)
        {
            await collection.AddAsync(item);
        }

        Console.WriteLine($"Added {largeTextData.Count} large items with GZip compression");

        // Verify data integrity
        var retrievedItems = new List<string>();
        await foreach (var item in collection.GetAllAsync())
        {
            retrievedItems.Add(item);
        }

        Console.WriteLine($"Retrieved {retrievedItems.Count} large items");
        
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

        // Get compression statistics
        var stats = await database.GetComprehensiveStatisticsAsync();
        Console.WriteLine($"Compression enabled: {stats.HasCompression}");
        Console.WriteLine($"Compression type: {stats.CompressionType}");
        Console.WriteLine($"Total storage size: {stats.TotalStorageSize} bytes");

        // Calculate approximate compression ratio
        var originalSize = largeTextData.Sum(item => System.Text.Encoding.UTF8.GetByteCount(item));
        var compressedSize = stats.TotalStorageSize;
        var compressionRatio = (double)compressedSize / originalSize;
        var savingsPercentage = (1 - compressionRatio) * 100;

        Console.WriteLine($"Original data size: {originalSize:N0} bytes");
        Console.WriteLine($"Compressed size: {compressedSize:N0} bytes");
        Console.WriteLine($"Compression ratio: {compressionRatio:P2}");
        Console.WriteLine($"Space savings: {savingsPercentage:F1}%");

        await database.DisposeAsync();
    }
}
