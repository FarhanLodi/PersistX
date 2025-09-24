using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PersistX.Storage;
using PersistX.Database;
using Microsoft.Extensions.Logging;
using PersistX.Models;

namespace PersistX.Test.Tests.AdvancedStorage;

/// <summary>
/// Test for Automated Backup & Restore functionality.
/// </summary>
public class BackupRestoreTest
{
    private static string GetDataPath(string fileName)
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "persistx_data");
        Directory.CreateDirectory(dataDir);
        var fullPath = Path.Combine(dataDir, fileName);
        Console.WriteLine($"📁 Database will be saved to: {Path.GetFullPath(fullPath)}");
        return fullPath;
    }

    public static async Task RunBackupRestoreTestAsync()
    {
        Console.WriteLine("=== Backup & Restore Test ===");
        
        // Clean up any existing test files
        var testDbPath = GetDataPath("backup_test.db");
        var backupPath = GetDataPath("backup_test_backups");
        if (Directory.Exists(testDbPath))
        {
            Directory.Delete(testDbPath, true);
        }
        if (Directory.Exists(backupPath))
        {
            Directory.Delete(backupPath, true);
        }

        try
        {
            // Test 1: Create backup
            Console.WriteLine("\n--- Test 1: Create Backup ---");
            var backupId = await TestCreateBackup(testDbPath);

            // Test 2: List backups
            Console.WriteLine("\n--- Test 2: List Backups ---");
            await TestListBackups(testDbPath);

            // Test 3: Restore from backup
            Console.WriteLine("\n--- Test 3: Restore from Backup ---");
            await TestRestoreFromBackup(testDbPath, backupId);

            // Test 4: Multiple backups
            Console.WriteLine("\n--- Test 4: Multiple Backups ---");
            await TestMultipleBackups(testDbPath);

            Console.WriteLine("\n✅ All backup & restore tests completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Backup & restore test failed! Error: {ex.Message}");
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
                if (Directory.Exists(backupPath))
                {
                    Directory.Delete(backupPath, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static async Task<string> TestCreateBackup(string dbPath)
    {
        // Create database with backup enabled
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = dbPath,
                ["EnableBackup"] = "true"
            }
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        var database = new PersistX.Database.Database("BackupTest", new FileStorage(loggerFactory.CreateLogger<FileStorage>()), config, loggerFactory.CreateLogger<PersistX.Database.Database>());
        await database.InitializeAsync();

        // Create collection and add some data
        var collection = await database.CreateCollectionAsync<string>("backup_test_items");

        var testItems = new[] { "backup_item_1", "backup_item_2", "backup_item_3", "backup_item_4", "backup_item_5" };
        foreach (var item in testItems)
        {
            await collection.AddAsync(item);
        }

        Console.WriteLine($"Added {testItems.Length} items before backup");

        // Create backup
        var backupId = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var backupMetadata = await database.CreateBackupAsync(backupId);

        Console.WriteLine($"Created backup: {backupId}");
        Console.WriteLine($"Backup size: {backupMetadata.SizeBytes} bytes");
        Console.WriteLine($"Backup created at: {backupMetadata.CreatedAt}");

        // Add more data after backup
        await collection.AddAsync("post_backup_item_1");
        await collection.AddAsync("post_backup_item_2");

        var itemsAfterBackup = new List<string>();
        await foreach (var item in collection.GetAllAsync())
        {
            itemsAfterBackup.Add(item);
        }

        Console.WriteLine($"Items after backup: {itemsAfterBackup.Count} (should be 7)");

        await database.DisposeAsync();
        return backupId;
    }

    private static async Task TestListBackups(string dbPath)
    {
        // Create database with backup enabled
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = dbPath,
                ["EnableBackup"] = "true"
            }
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        var database = new PersistX.Database.Database("BackupTest", new FileStorage(loggerFactory.CreateLogger<FileStorage>()), config, loggerFactory.CreateLogger<PersistX.Database.Database>());
        await database.InitializeAsync();

        // List all backups
        var backups = new List<BackupMetadata>();
        await foreach (var backup in database.ListBackupsAsync())
        {
            backups.Add(backup);
        }

        Console.WriteLine($"Found {backups.Count} backups:");
        foreach (var backup in backups)
        {
            Console.WriteLine($"  - {backup.BackupId}: {backup.SizeBytes} bytes, created at {backup.CreatedAt}");
        }

        if (backups.Count == 0)
        {
            throw new InvalidOperationException("No backups found");
        }

        await database.DisposeAsync();
    }

    private static async Task TestRestoreFromBackup(string dbPath, string backupId)
    {
        // Create database with backup enabled
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = dbPath,
                ["EnableBackup"] = "true"
            }
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });

        var database = new PersistX.Database.Database("BackupTest", new FileStorage(loggerFactory.CreateLogger<FileStorage>()), config, loggerFactory.CreateLogger<PersistX.Database.Database>());
        await database.InitializeAsync();

        // Restore from backup
        await database.RestoreBackupAsync(backupId);
        Console.WriteLine($"Restored from backup: {backupId}");

        // Debug: Check what locations exist after restore
        Console.WriteLine("Locations after restore:");
        await foreach (var location in database.Backend.ListLocationsAsync())
        {
            var size = await database.Backend.GetSizeAsync(location);
            Console.WriteLine($"  - {location}: {size} bytes");
        }

        // Debug: Check if collection file exists after restore
        var dataLocation = "backup_test_items.data";
        var exists = await database.Backend.ExistsAsync(dataLocation);
        Console.WriteLine($"Collection file exists after restore: {exists}");
        
        if (exists)
        {
            var size = await database.Backend.GetSizeAsync(dataLocation);
            Console.WriteLine($"Collection file size: {size} bytes");
            
            // Try to read and deserialize the data manually
            try
            {
                var data = await database.Backend.ReadAsync(dataLocation, 0, (int)size);
                Console.WriteLine($"Raw data length: {data.Length} bytes");
                
                // Show raw data as text for debugging
                var text = System.Text.Encoding.UTF8.GetString(data.Span);
                Console.WriteLine($"Raw data (first 200 chars): {text.Substring(0, Math.Min(200, text.Length))}");
                
                // Try to deserialize as JSON
                var serializer = new PersistX.Serialization.JsonSerializer<List<string>>();
                var items = await serializer.DeserializeAsync(data);
                Console.WriteLine($"Deserialized items count: {items.Count}");
                foreach (var item in items)
                {
                    Console.WriteLine($"  - {item}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to deserialize data: {ex.Message}");
            }
        }

        // Verify restored data
        var collection = await database.GetCollectionAsync<string>("backup_test_items");
        if (collection != null)
        {
            var restoredItems = new List<string>();
            await foreach (var item in collection.GetAllAsync())
            {
                restoredItems.Add(item);
            }

            Console.WriteLine($"Restored {restoredItems.Count} items from backup");
            
            // Should have 5 items (the original backup), not 7 (which included post-backup items)
            if (restoredItems.Count != 5)
            {
                throw new InvalidOperationException($"Expected 5 items after restore, but got {restoredItems.Count}");
            }

            // Verify specific items
            var expectedItems = new[] { "backup_item_1", "backup_item_2", "backup_item_3", "backup_item_4", "backup_item_5" };
            for (int i = 0; i < expectedItems.Length; i++)
            {
                if (restoredItems[i] != expectedItems[i])
                {
                    throw new InvalidOperationException($"Restored item {i} doesn't match expected value");
                }
            }

            Console.WriteLine("All restored items match expected values");
        }
        else
        {
            throw new InvalidOperationException("Failed to restore collection from backup");
        }

        await database.DisposeAsync();
    }

    private static async Task TestMultipleBackups(string dbPath)
    {
        // Create database with backup enabled
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = dbPath,
                ["EnableBackup"] = "true"
            }
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        var database = new PersistX.Database.Database("BackupTest", new FileStorage(loggerFactory.CreateLogger<FileStorage>()), config, loggerFactory.CreateLogger<PersistX.Database.Database>());
        await database.InitializeAsync();

        var collection = await database.CreateCollectionAsync<string>("multiple_backup_items");

        // Create multiple backups with different data
        var backupIds = new List<string>();

        // Backup 1: Initial data
        await collection.AddAsync("backup1_item_1");
        await collection.AddAsync("backup1_item_2");
        var backup1Id = $"backup1_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        await database.CreateBackupAsync(backup1Id);
        backupIds.Add(backup1Id);
        Console.WriteLine($"Created backup 1: {backup1Id}");

        // Backup 2: More data
        await collection.AddAsync("backup2_item_1");
        await collection.AddAsync("backup2_item_2");
        var backup2Id = $"backup2_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        await database.CreateBackupAsync(backup2Id);
        backupIds.Add(backup2Id);
        Console.WriteLine($"Created backup 2: {backup2Id}");

        // Backup 3: Even more data
        await collection.AddAsync("backup3_item_1");
        await collection.AddAsync("backup3_item_2");
        var backup3Id = $"backup3_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        await database.CreateBackupAsync(backup3Id);
        backupIds.Add(backup3Id);
        Console.WriteLine($"Created backup 3: {backup3Id}");

        // List all backups
        var allBackups = new List<BackupMetadata>();
        await foreach (var backup in database.ListBackupsAsync())
        {
            allBackups.Add(backup);
        }

        Console.WriteLine($"Total backups created: {allBackups.Count}");
        
        if (allBackups.Count < 3)
        {
            throw new InvalidOperationException($"Expected at least 3 backups, but found {allBackups.Count}");
        }

        // Test restoring to different backup points
        Console.WriteLine("Testing restore to backup 1...");
        await database.RestoreBackupAsync(backup1Id);
        var restoredCollection1 = await database.GetCollectionAsync<string>("multiple_backup_items");
        if (restoredCollection1 == null)
        {
            throw new InvalidOperationException("Failed to get collection after restore to backup 1");
        }
        
        var itemsAfterRestore1 = new List<string>();
        await foreach (var item in restoredCollection1.GetAllAsync())
        {
            itemsAfterRestore1.Add(item);
        }
        Console.WriteLine($"Items after restore to backup 1: {itemsAfterRestore1.Count} (should be 2)");

        if (itemsAfterRestore1.Count != 2)
        {
            throw new InvalidOperationException($"Expected 2 items after restore to backup 1, but got {itemsAfterRestore1.Count}");
        }

        Console.WriteLine("Testing restore to backup 2...");
        await database.RestoreBackupAsync(backup2Id);
        var restoredCollection2 = await database.GetCollectionAsync<string>("multiple_backup_items");
        if (restoredCollection2 == null)
        {
            throw new InvalidOperationException("Failed to get collection after restore to backup 2");
        }
        
        var itemsAfterRestore2 = new List<string>();
        await foreach (var item in restoredCollection2.GetAllAsync())
        {
            itemsAfterRestore2.Add(item);
        }
        Console.WriteLine($"Items after restore to backup 2: {itemsAfterRestore2.Count} (should be 4)");

        if (itemsAfterRestore2.Count != 4)
        {
            throw new InvalidOperationException($"Expected 4 items after restore to backup 2, but got {itemsAfterRestore2.Count}");
        }

        Console.WriteLine("Testing restore to backup 3...");
        await database.RestoreBackupAsync(backup3Id);
        var restoredCollection3 = await database.GetCollectionAsync<string>("multiple_backup_items");
        if (restoredCollection3 == null)
        {
            throw new InvalidOperationException("Failed to get collection after restore to backup 3");
        }
        
        var itemsAfterRestore3 = new List<string>();
        await foreach (var item in restoredCollection3.GetAllAsync())
        {
            itemsAfterRestore3.Add(item);
        }
        Console.WriteLine($"Items after restore to backup 3: {itemsAfterRestore3.Count} (should be 6)");

        if (itemsAfterRestore3.Count != 6)
        {
            throw new InvalidOperationException($"Expected 6 items after restore to backup 3, but got {itemsAfterRestore3.Count}");
        }

        await database.DisposeAsync();
    }
}
