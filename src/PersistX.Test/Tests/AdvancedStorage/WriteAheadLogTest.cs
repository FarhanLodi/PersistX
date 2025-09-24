using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PersistX.Storage;
using PersistX.Database;
using Microsoft.Extensions.Logging;
using PersistX.Test.Utils;

namespace PersistX.Test.Tests.AdvancedStorage;

/// <summary>
/// Test for Write-Ahead Logging (WAL) functionality.
/// </summary>
public class WriteAheadLogTest : TestBase
{
    public WriteAheadLogTest() : base("Write-Ahead Logging Test") { }

    protected override async Task ExecuteTestAsync()
    {
        // Clean up any existing test files
        var testDbPath = TestHelper.GetTestDataPath("wal_test.db");
        if (Directory.Exists(testDbPath))
        {
            Directory.Delete(testDbPath, true);
        }

        try
        {
            // Test 1: Basic WAL functionality
            TestHelper.DisplaySectionHeader("Basic WAL Operations");
            await TestBasicWalOperations(testDbPath);

            // Test 2: Transaction logging
            TestHelper.DisplaySectionHeader("Transaction Logging");
            await TestTransactionLogging(testDbPath);

            // Test 3: Crash recovery simulation
            TestHelper.DisplaySectionHeader("Crash Recovery Simulation");
            await TestCrashRecovery(testDbPath);
        }
        finally
        {
            // Clean up test files
            if (Directory.Exists(testDbPath))
            {
                Directory.Delete(testDbPath, true);
            }
        }
    }

    public static async Task RunWalTestAsync()
    {
        await new WriteAheadLogTest().RunTestAsync();
    }

    private async Task TestBasicWalOperations(string testDbPath)
    {
        // Create database with WAL enabled
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = testDbPath,
                ["EnableWAL"] = "true"
            }
        };

        var database = new PersistX.Database.Database(
            "WalTest",
            new FileStorage(LoggerFactory.CreateLogger<FileStorage>()),
            config,
            LoggerFactory.CreateLogger<PersistX.Database.Database>());

        await database.InitializeAsync();

        // Create a collection and add some items
        var collection = await database.CreateCollectionAsync<string>("wal_test_items");
        
        Console.WriteLine("Adding items with WAL enabled...");
        await collection.AddAsync("Item 1");
        await collection.AddAsync("Item 2");
        await collection.AddAsync("Item 3");

        // Verify items were added
        var count = await collection.CountAsync;
        Console.WriteLine($"Collection count: {count}");

        if (count != 3)
        {
            throw new InvalidOperationException($"Expected 3 items, but got {count}");
        }

        // Check if WAL files exist
        var walDir = Path.Combine(Path.GetDirectoryName(testDbPath)!, "persistx_wal", "WalTest");
        if (Directory.Exists(walDir))
        {
            var walFiles = Directory.GetFiles(walDir);
            Console.WriteLine($"WAL files found: {string.Join(", ", walFiles.Select(Path.GetFileName))}");
        }

        await database.DisposeAsync();
        Console.WriteLine("✅ Basic WAL operations completed successfully");
    }

    private async Task TestTransactionLogging(string testDbPath)
    {
        // Create database with WAL enabled
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = testDbPath,
                ["EnableWAL"] = "true"
            }
        };

        var database = new PersistX.Database.Database(
            "WalTransactionTest",
            new FileStorage(LoggerFactory.CreateLogger<FileStorage>()),
            config,
            LoggerFactory.CreateLogger<PersistX.Database.Database>());

        await database.InitializeAsync();

        // Test transaction with WAL
        var collection = await database.CreateCollectionAsync<string>("wal_transaction_items");
        
        Console.WriteLine("Testing transaction with WAL...");
        
        await using var transaction = await database.TransactionManager.BeginTransactionAsync();
        try
        {
            await collection.AddAsync("Transaction Item 1");
            await collection.AddAsync("Transaction Item 2");
            
            // Commit transaction
            await transaction.CommitAsync();
            Console.WriteLine("Transaction committed successfully");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // Verify items were added
        var count = await collection.CountAsync;
        Console.WriteLine($"Collection count after transaction: {count}");

        if (count != 2)
        {
            throw new InvalidOperationException($"Expected 2 items, but got {count}");
        }

        await database.DisposeAsync();
        Console.WriteLine("✅ Transaction logging completed successfully");
    }

    private async Task TestCrashRecovery(string testDbPath)
    {
        // Create first database instance
        var config1 = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = testDbPath,
                ["EnableWAL"] = "true"
            }
        };

        var database1 = new PersistX.Database.Database(
            "WalCrashTest",
            new FileStorage(LoggerFactory.CreateLogger<FileStorage>()),
            config1,
            LoggerFactory.CreateLogger<PersistX.Database.Database>());

        await database1.InitializeAsync();

        // Add some items
        var collection = await database1.CreateCollectionAsync<string>("wal_crash_items");
        await collection.AddAsync("Crash Item 1");
        await collection.AddAsync("Crash Item 2");
        await collection.AddAsync("Crash Item 3");

        Console.WriteLine("Items added before simulated crash");
        await database1.DisposeAsync();

        // Simulate crash by creating a new database instance
        var config2 = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = testDbPath,
                ["EnableWAL"] = "true"
            }
        };

        var database2 = new PersistX.Database.Database(
            "WalCrashTest",
            new FileStorage(LoggerFactory.CreateLogger<FileStorage>()),
            config2,
            LoggerFactory.CreateLogger<PersistX.Database.Database>());

        await database2.InitializeAsync();

        // Try to recover the collection
        var recoveredCollection = await database2.GetCollectionAsync<string>("wal_crash_items");
        if (recoveredCollection == null)
        {
            throw new InvalidOperationException("Failed to recover collection after simulated crash");
        }

        var count = await recoveredCollection.CountAsync;
        Console.WriteLine($"Recovered collection count: {count}");

        if (count != 3)
        {
            throw new InvalidOperationException("Failed to recover collection after simulated crash");
        }

        await database2.DisposeAsync();
    }
}