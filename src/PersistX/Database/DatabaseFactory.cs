using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;
using PersistX.Storage;

namespace PersistX.Database;

/// <summary>
/// Factory for creating PersistX database instances.
/// </summary>
public static class DatabaseFactory
{
    /// <summary>
    /// Creates a new database instance with the specified configuration.
    /// </summary>
    /// <param name="name">The name of the database</param>
    /// <param name="configuration">Database configuration</param>
    /// <param name="serviceProvider">Optional service provider for dependency injection</param>
    /// <returns>A new database instance</returns>
    public static async Task<IDatabase> CreateAsync(
        string name,
        DatabaseConfiguration configuration,
        IServiceProvider? serviceProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configuration);

        var backend = CreateBackend(configuration.BackendType, configuration.BackendConfiguration, serviceProvider);
        var logger = serviceProvider?.GetService<ILogger<Database>>();

        var database = new Database(name, backend, configuration, logger);
        await database.InitializeAsync();

        return database;
    }

    /// <summary>
    /// Creates a new database instance with file backend.
    /// </summary>
    /// <param name="name">The name of the database</param>
    /// <param name="filePath">Path to the database file</param>
    /// <param name="serviceProvider">Optional service provider for dependency injection</param>
    /// <returns>A new database instance</returns>
    public static async Task<IDatabase> CreateFileDatabaseAsync(
        string name,
        string filePath,
        IServiceProvider? serviceProvider = null)
    {
        var configuration = new DatabaseConfiguration
        {
            BackendType = BackendType.File,
            BackendConfiguration = new Dictionary<string, string>
            {
                ["FilePath"] = filePath
            }
        };

        return await CreateAsync(name, configuration, serviceProvider);
    }

    /// <summary>
    /// Creates a new database instance with in-memory backend.
    /// </summary>
    /// <param name="name">The name of the database</param>
    /// <param name="serviceProvider">Optional service provider for dependency injection</param>
    /// <returns>A new database instance</returns>
    public static async Task<IDatabase> CreateInMemoryDatabaseAsync(
        string name,
        IServiceProvider? serviceProvider = null)
    {
        var configuration = new DatabaseConfiguration
        {
            BackendType = BackendType.InMemory
        };

        return await CreateAsync(name, configuration, serviceProvider);
    }

    private static IBackend CreateBackend(
        BackendType backendType,
        Dictionary<string, string>? configuration,
        IServiceProvider? serviceProvider)
    {
        var logger = serviceProvider?.GetService<ILogger<IBackend>>();
        
        return backendType switch
        {
            BackendType.File => new FileStorage(logger as ILogger<FileStorage>),
            BackendType.InMemory => new MemoryStorage(logger as ILogger<MemoryStorage>),
            BackendType.SQLite => new SQLiteStorage(logger as ILogger<SQLiteStorage>),
            _ => throw new NotSupportedException($"Backend type {backendType} is not supported")
        };
    }
}

/// <summary>
/// Configuration for a PersistX database.
/// </summary>
public class DatabaseConfiguration
{
    /// <summary>
    /// Gets or sets the backend type to use.
    /// </summary>
    public BackendType BackendType { get; set; } = BackendType.File;

    /// <summary>
    /// Gets or sets the backend-specific configuration.
    /// </summary>
    public Dictionary<string, string>? BackendConfiguration { get; set; }

    /// <summary>
    /// Gets or sets the default page size in bytes.
    /// </summary>
    public int PageSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the maximum cache size in bytes.
    /// </summary>
    public long MaxCacheSize { get; set; } = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Gets or sets whether to enable write-ahead logging.
    /// </summary>
    public bool EnableWAL { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable compression.
    /// </summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>
    /// Gets or sets the compression algorithm to use.
    /// </summary>
    public CompressionAlgorithm CompressionAlgorithm { get; set; } = CompressionAlgorithm.Snappy;

    /// <summary>
    /// Gets or sets the default serialization format.
    /// </summary>
    public string DefaultSerializationFormat { get; set; } = SerializationFormats.MessagePack;

    /// <summary>
    /// Gets or sets whether to enable automatic maintenance.
    /// </summary>
    public bool EnableAutoMaintenance { get; set; } = true;

    /// <summary>
    /// Gets or sets the maintenance interval.
    /// </summary>
    public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Supported backend types.
/// </summary>
public enum BackendType
{
    /// <summary>
    /// File-based backend.
    /// </summary>
    File,

    /// <summary>
    /// In-memory backend.
    /// </summary>
    InMemory,

    /// <summary>
    /// SQLite backend.
    /// </summary>
    SQLite,

    /// <summary>
    /// Cloud storage backend (S3, Azure Blob, etc.).
    /// </summary>
    Cloud
}

/// <summary>
/// Supported compression algorithms.
/// </summary>
public enum CompressionAlgorithm
{
    /// <summary>
    /// No compression.
    /// </summary>
    None,

    /// <summary>
    /// Snappy compression.
    /// </summary>
    Snappy,

    /// <summary>
    /// Zstandard compression.
    /// </summary>
    Zstd,

    /// <summary>
    /// GZip compression.
    /// </summary>
    GZip,

    /// <summary>
    /// LZ4 compression.
    /// </summary>
    LZ4
}
