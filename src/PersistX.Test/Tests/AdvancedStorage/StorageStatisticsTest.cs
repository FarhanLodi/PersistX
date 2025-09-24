using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PersistX.Storage;
using PersistX.Database;
using Microsoft.Extensions.Logging;
using PersistX.Enums;
using PersistX.Models;
using PersistX.Encryption;

namespace PersistX.Test.Tests.AdvancedStorage;

/// <summary>
/// Test for Storage Statistics functionality.
/// </summary>
public class StorageStatisticsTest
{
    private static string GetDataPath(string fileName)
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "persistx_data");
        Directory.CreateDirectory(dataDir);
        var fullPath = Path.Combine(dataDir, fileName);
        Console.WriteLine($"📁 Database will be saved to: {Path.GetFullPath(fullPath)}");
        return fullPath;
    }

    public static async Task RunStorageStatisticsTestAsync()
    {
        Console.WriteLine("=== Storage Statistics Test ===");

        try
        {
            // Test 1: Basic statistics
            Console.WriteLine("\n--- Test 1: Basic Statistics ---");
            var testDbPath1 = GetDataPath("statistics_test_1.db");
            await TestBasicStatistics(testDbPath1);

            // Test 2: Statistics with advanced features
            Console.WriteLine("\n--- Test 2: Statistics with Advanced Features ---");
            var testDbPath2 = GetDataPath("statistics_test_2.db");
            await TestAdvancedStatistics(testDbPath2);

            // Test 3: Statistics monitoring
            Console.WriteLine("\n--- Test 3: Statistics Monitoring ---");
            var testDbPath3 = GetDataPath("statistics_test_3.db");
            await TestStatisticsMonitoring(testDbPath3);

            Console.WriteLine("\n✅ All storage statistics tests completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Storage statistics test failed! Error: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static async Task TestBasicStatistics(string dbPath)
    {
        // Create database
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = dbPath
            }
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        var database = new PersistX.Database.Database("StatisticsTest", new FileStorage(loggerFactory.CreateLogger<FileStorage>()), config, loggerFactory.CreateLogger<PersistX.Database.Database>());
        await database.InitializeAsync();

        // Get initial statistics
        var initialStats = await database.GetStatisticsAsync();
        Console.WriteLine("Initial statistics:");
        Console.WriteLine($"  Collection count: {initialStats.CollectionCount}");
        Console.WriteLine($"  Active transaction count: {initialStats.ActiveTransactionCount}");
        Console.WriteLine($"  Total storage size: {initialStats.TotalStorageSize} bytes");
        Console.WriteLine($"  Created at: {initialStats.CreatedAt}");
        Console.WriteLine($"  Last maintenance: {initialStats.LastMaintenance}");

        // Create collections and add data
        var collection1 = await database.CreateCollectionAsync<string>("stats_collection_1");
        var collection2 = await database.CreateCollectionAsync<string>("stats_collection_2");

        // Add items to collections
        for (int i = 0; i < 10; i++)
        {
            await collection1.AddAsync($"item_{i}_collection_1");
            await collection2.AddAsync($"item_{i}_collection_2");
        }

        // Get statistics after adding data
        var statsAfterData = await database.GetStatisticsAsync();
        Console.WriteLine("\nStatistics after adding data:");
        Console.WriteLine($"  Collection count: {statsAfterData.CollectionCount}");
        Console.WriteLine($"  Active transaction count: {statsAfterData.ActiveTransactionCount}");
        Console.WriteLine($"  Total storage size: {statsAfterData.TotalStorageSize} bytes");

        if (statsAfterData.CollectionCount != 2)
        {
            throw new InvalidOperationException($"Expected 2 collections, but got {statsAfterData.CollectionCount}");
        }

        if (statsAfterData.TotalStorageSize <= initialStats.TotalStorageSize)
        {
            throw new InvalidOperationException("Storage size should have increased after adding data");
        }

        // Test transaction statistics
        await database.ExecuteInTransactionAsync(async transaction =>
        {
            await collection1.AddAsync("transaction_item_1");
            await collection1.AddAsync("transaction_item_2");
        });

        var statsAfterTransaction = await database.GetStatisticsAsync();
        Console.WriteLine($"\nActive transactions after transaction: {statsAfterTransaction.ActiveTransactionCount} (should be 0)");

        if (statsAfterTransaction.ActiveTransactionCount != 0)
        {
            throw new InvalidOperationException("Active transaction count should be 0 after transaction completion");
        }

        await database.DisposeAsync();
    }

    private static async Task TestAdvancedStatistics(string dbPath)
    {
        // Generate encryption key
        var encryptionProvider = new PersistX.Encryption.AesEncryptionProvider();
        var encryptionKey = await encryptionProvider.GenerateKeyAsync();
        var encryptionKeyBase64 = Convert.ToBase64String(encryptionKey);

        // Create database with advanced features
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = dbPath,
                ["EnableWAL"] = "true",
                ["CompressionType"] = "GZip",
                ["EncryptionType"] = "Aes",
                ["EncryptionKey"] = encryptionKeyBase64,
                ["EnableBackup"] = "true"
            }
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        var database = new PersistX.Database.Database($"AdvancedStatisticsTest_{Guid.NewGuid():N}", new FileStorage(loggerFactory.CreateLogger<FileStorage>()), config, loggerFactory.CreateLogger<PersistX.Database.Database>());
        await database.InitializeAsync();

        // Get comprehensive statistics
        var comprehensiveStats = await database.GetComprehensiveStatisticsAsync();
        Console.WriteLine("Comprehensive statistics with advanced features:");
        Console.WriteLine($"  Collection count: {comprehensiveStats.CollectionCount}");
        Console.WriteLine($"  Active transaction count: {comprehensiveStats.ActiveTransactionCount}");
        Console.WriteLine($"  Total storage size: {comprehensiveStats.TotalStorageSize} bytes");
        Console.WriteLine($"  Has Write-Ahead Log: {comprehensiveStats.HasWriteAheadLog}");
        Console.WriteLine($"  Has Compression: {comprehensiveStats.HasCompression}");
        Console.WriteLine($"  Has Encryption: {comprehensiveStats.HasEncryption}");
        Console.WriteLine($"  Has Backup: {comprehensiveStats.HasBackup}");
        Console.WriteLine($"  Compression type: {comprehensiveStats.CompressionType}");
        Console.WriteLine($"  Encryption type: {comprehensiveStats.EncryptionType}");
        Console.WriteLine($"  WAL size: {comprehensiveStats.WalSizeBytes} bytes");

        // Verify advanced features are enabled
        if (!comprehensiveStats.HasWriteAheadLog)
        {
            throw new InvalidOperationException("Write-Ahead Log should be enabled");
        }

        if (!comprehensiveStats.HasCompression)
        {
            throw new InvalidOperationException("Compression should be enabled");
        }

        if (!comprehensiveStats.HasEncryption)
        {
            throw new InvalidOperationException("Encryption should be enabled");
        }

        if (!comprehensiveStats.HasBackup)
        {
            throw new InvalidOperationException("Backup should be enabled");
        }

        if (comprehensiveStats.CompressionType != CompressionType.GZip)
        {
            throw new InvalidOperationException($"Expected GZip compression, but got {comprehensiveStats.CompressionType}");
        }

        if (comprehensiveStats.EncryptionType != EncryptionType.Aes)
        {
            throw new InvalidOperationException($"Expected AES encryption, but got {comprehensiveStats.EncryptionType}");
        }

        // Add data and monitor statistics changes
        var collection = await database.CreateCollectionAsync<string>("advanced_stats_collection");

        var largeTextData = new List<string>();
        for (int i = 0; i < 50; i++)
        {
            var largeText = $"Large text item {i} with repetitive content for compression testing. " +
                           $"This text is designed to test compression effectiveness. " +
                           $"The more repetitive the content, the better the compression ratio. " +
                           $"Compression algorithms work best with redundant data patterns. " +
                           $"This is item {i} of 50 items designed to test statistics monitoring.";
            largeTextData.Add(largeText);
        }

        foreach (var item in largeTextData)
        {
            await collection.AddAsync(item);
        }

        // Get statistics after adding large data
        var statsAfterLargeData = await database.GetComprehensiveStatisticsAsync();
        Console.WriteLine($"\nStatistics after adding large data:");
        Console.WriteLine($"  Total storage size: {statsAfterLargeData.TotalStorageSize} bytes");
        Console.WriteLine($"  WAL size: {statsAfterLargeData.WalSizeBytes} bytes");

        // Create a backup to test backup statistics
        var backupId = $"stats_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var backupMetadata = await database.CreateBackupAsync(backupId);
        Console.WriteLine($"Created backup: {backupId} ({backupMetadata.SizeBytes} bytes)");

        // List backups
        var backups = new List<BackupMetadata>();
        await foreach (var backup in database.ListBackupsAsync())
        {
            backups.Add(backup);
        }
        Console.WriteLine($"Total backups: {backups.Count}");

        await database.DisposeAsync();
    }

    private static async Task TestStatisticsMonitoring(string dbPath)
    {
        // Create database for monitoring
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = dbPath,
                ["EnableWAL"] = "true",
                ["CompressionType"] = "Deflate"
            }
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        var database = new PersistX.Database.Database($"StatisticsMonitoringTest_{Guid.NewGuid():N}", new FileStorage(loggerFactory.CreateLogger<FileStorage>()), config, loggerFactory.CreateLogger<PersistX.Database.Database>());
        await database.InitializeAsync();

        var collection = await database.CreateCollectionAsync<string>("monitoring_collection");

        // Monitor statistics over time
        var statisticsHistory = new List<ComprehensiveDatabaseStatistics>();

        for (int batch = 0; batch < 5; batch++)
        {
            // Add a batch of items
            for (int i = 0; i < 20; i++)
            {
                await collection.AddAsync($"monitoring_item_{batch}_{i}");
            }

            // Get statistics after each batch
            var stats = await database.GetComprehensiveStatisticsAsync();
            statisticsHistory.Add(stats);

            Console.WriteLine($"Batch {batch + 1}: {stats.CollectionCount} collections, {stats.TotalStorageSize} bytes, WAL: {stats.WalSizeBytes} bytes");
        }

        // Verify statistics are increasing
        for (int i = 1; i < statisticsHistory.Count; i++)
        {
            if (statisticsHistory[i].TotalStorageSize <= statisticsHistory[i - 1].TotalStorageSize)
            {
                throw new InvalidOperationException($"Storage size should be increasing, but batch {i} has {statisticsHistory[i].TotalStorageSize} <= batch {i - 1} has {statisticsHistory[i - 1].TotalStorageSize}");
            }
        }

        Console.WriteLine("Statistics monitoring completed successfully - all metrics increased as expected");

        // Test maintenance
        Console.WriteLine("Running maintenance...");
        await database.MaintenanceAsync();
        
        var statsAfterMaintenance = await database.GetComprehensiveStatisticsAsync();
        Console.WriteLine($"Statistics after maintenance: {statsAfterMaintenance.TotalStorageSize} bytes");

        await database.DisposeAsync();
    }
}
