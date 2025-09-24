using Microsoft.Extensions.Logging;
using PersistX.Database;
using PersistX.Storage;

namespace PersistX.Test.Utils;

/// <summary>
/// Base class for all PersistX tests providing common functionality.
/// </summary>
public abstract class TestBase
{
    protected readonly ILoggerFactory LoggerFactory;
    protected readonly string TestName;

    protected TestBase(string testName, LogLevel logLevel = LogLevel.Information)
    {
        TestName = testName;
        LoggerFactory = TestHelper.CreateLoggerFactory(logLevel);
    }

    /// <summary>
    /// Runs the test with proper setup and teardown.
    /// </summary>
    public async Task RunTestAsync()
    {
        try
        {
            TestHelper.DisplayTestHeader(TestName);
            await ExecuteTestAsync();
            TestHelper.DisplayTestSuccess(TestName);
        }
        catch (Exception ex)
        {
            TestHelper.DisplayTestFailure(TestName, ex);
            throw;
        }
        finally
        {
            await CleanupAsync();
            LoggerFactory?.Dispose();
        }
    }

    /// <summary>
    /// Executes the actual test logic. Override this method in derived classes.
    /// </summary>
    protected abstract Task ExecuteTestAsync();

    /// <summary>
    /// Performs cleanup after the test. Override this method in derived classes.
    /// </summary>
    protected virtual Task CleanupAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a database with the specified configuration.
    /// </summary>
    /// <param name="databaseName">The name of the database</param>
    /// <param name="filePath">The file path for the database</param>
    /// <param name="configuration">Optional configuration</param>
    /// <returns>Database instance</returns>
    protected async Task<PersistX.Database.Database> CreateDatabaseAsync(string databaseName, string filePath, DatabaseConfiguration? configuration = null)
    {
        configuration ??= new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = filePath
            }
        };

        var database = new PersistX.Database.Database(
            databaseName,
            new FileStorage(LoggerFactory.CreateLogger<FileStorage>()),
            configuration,
            LoggerFactory.CreateLogger<PersistX.Database.Database>());

        await database.InitializeAsync();
        return database;
    }

    /// <summary>
    /// Creates a database with advanced storage features enabled.
    /// </summary>
    /// <param name="databaseName">The name of the database</param>
    /// <param name="filePath">The file path for the database</param>
    /// <param name="enableWal">Enable Write-Ahead Logging</param>
    /// <param name="compressionType">Compression type (None, GZip, Deflate)</param>
    /// <param name="encryptionType">Encryption type (None, Aes)</param>
    /// <param name="encryptionKey">Encryption key (required if encryption is enabled)</param>
    /// <param name="enableBackup">Enable backup functionality</param>
    /// <param name="enableMemoryMappedIO">Enable memory-mapped I/O</param>
    /// <returns>Database instance</returns>
    protected async Task<PersistX.Database.Database> CreateAdvancedDatabaseAsync(
        string databaseName,
        string filePath,
        bool enableWal = false,
        string compressionType = "None",
        string encryptionType = "None",
        string? encryptionKey = null,
        bool enableBackup = false,
        bool enableMemoryMappedIO = false)
    {
        var config = new DatabaseConfiguration
        {
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = filePath,
                ["EnableWAL"] = enableWal.ToString().ToLowerInvariant(),
                ["CompressionType"] = compressionType,
                ["EncryptionType"] = encryptionType,
                ["EnableBackup"] = enableBackup.ToString().ToLowerInvariant(),
                ["EnableMemoryMappedIO"] = enableMemoryMappedIO.ToString().ToLowerInvariant()
            }
        };

        if (!string.IsNullOrEmpty(encryptionKey))
        {
            config.BackendConfiguration["EncryptionKey"] = encryptionKey;
        }

        return await CreateDatabaseAsync(databaseName, filePath, config);
    }

    /// <summary>
    /// Measures the execution time of an async operation.
    /// </summary>
    /// <param name="operation">The operation to measure</param>
    /// <param name="operationName">The name of the operation</param>
    /// <returns>The execution time</returns>
    protected async Task<TimeSpan> MeasureExecutionTimeAsync(Func<Task> operation, string operationName)
    {
        var startTime = DateTime.UtcNow;
        await operation();
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;
        
        Console.WriteLine($"⏱️ {operationName} completed in {TestHelper.FormatTimeSpan(duration)}");
        return duration;
    }

    /// <summary>
    /// Measures the execution time of an async operation that returns a value.
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="operation">The operation to measure</param>
    /// <param name="operationName">The name of the operation</param>
    /// <returns>A tuple containing the result and execution time</returns>
    protected async Task<(T Result, TimeSpan Duration)> MeasureExecutionTimeAsync<T>(Func<Task<T>> operation, string operationName)
    {
        var startTime = DateTime.UtcNow;
        var result = await operation();
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;
        
        Console.WriteLine($"⏱️ {operationName} completed in {TestHelper.FormatTimeSpan(duration)}");
        return (result, duration);
    }
}
