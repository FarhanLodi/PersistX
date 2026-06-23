using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersistX.Database;
using PersistX.Interfaces;
using PersistX.Storage;

namespace PersistX.Extensions;

/// <summary>
/// Configuration options for the PersistX DI registration.
/// </summary>
public class PersistXOptions
{
    /// <summary>
    /// Gets or sets the database name. Defaults to <c>"persistx"</c>.
    /// </summary>
    public string DatabaseName { get; set; } = "persistx";

    /// <summary>
    /// Gets or sets the file path used by the file-based backend. Defaults to <c>"./persistx_data"</c>.
    /// </summary>
    public string FilePath { get; set; } = "./persistx_data";

    /// <summary>
    /// Gets or sets the storage backend type. Defaults to <see cref="PersistXBackendType.File"/>.
    /// </summary>
    public PersistXBackendType Backend { get; set; } = PersistXBackendType.File;

    /// <summary>
    /// Gets or sets whether to enable write-ahead logging. Defaults to <c>false</c>.
    /// </summary>
    public bool EnableWal { get; set; } = false;
}

/// <summary>
/// The storage backend type to use when registering PersistX via <see cref="ServiceCollectionExtensions.AddPersistX"/>.
/// </summary>
public enum PersistXBackendType
{
    /// <summary>File-based backend.</summary>
    File,

    /// <summary>In-memory backend (data is not persisted across restarts; useful for testing).</summary>
    Memory,

    /// <summary>SQLite backend.</summary>
    SQLite
}

/// <summary>
/// Extension methods for registering PersistX services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers PersistX with a database configured via <paramref name="configure"/>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">An optional callback to configure <see cref="PersistXOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// The factory methods on <see cref="DatabaseFactory"/> are asynchronous. Because DI registration is
    /// synchronous, this overload uses <c>.GetAwaiter().GetResult()</c> to block until the database is
    /// ready. Avoid calling this inside an already-running async context to prevent deadlocks; prefer the
    /// specific <c>AddPersistX*</c> overloads when you need more control.
    /// </remarks>
    public static IServiceCollection AddPersistX(
        this IServiceCollection services,
        Action<PersistXOptions>? configure = null)
    {
        var options = new PersistXOptions();
        configure?.Invoke(options);

        services.AddSingleton<IDatabase>(sp =>
        {
            return options.Backend switch
            {
                PersistXBackendType.Memory =>
                    DatabaseFactory.CreateInMemoryDatabaseAsync(options.DatabaseName, sp)
                        .GetAwaiter()
                        .GetResult(),

                PersistXBackendType.SQLite =>
                    DatabaseFactory.CreateAsync(
                        options.DatabaseName,
                        new DatabaseConfiguration
                        {
                            BackendType = BackendType.SQLite,
                            EnableWAL = options.EnableWal
                        },
                        sp)
                        .GetAwaiter()
                        .GetResult(),

                _ /* File */ =>
                    DatabaseFactory.CreateFileDatabaseAsync(options.DatabaseName, options.FilePath, sp)
                        .GetAwaiter()
                        .GetResult()
            };
        });

        return services;
    }

    /// <summary>
    /// Registers a file-backed <see cref="IDatabase"/> as a singleton.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="name">The database name.</param>
    /// <param name="filePath">The path to the database file directory.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// <see cref="DatabaseFactory.CreateFileDatabaseAsync"/> is asynchronous; this method blocks via
    /// <c>.GetAwaiter().GetResult()</c> to satisfy the synchronous DI registration contract.
    /// </remarks>
    public static IServiceCollection AddPersistXFileDatabase(
        this IServiceCollection services,
        string name,
        string filePath)
    {
        services.AddSingleton<IDatabase>(sp =>
            DatabaseFactory.CreateFileDatabaseAsync(name, filePath, sp)
                .GetAwaiter()
                .GetResult());

        return services;
    }

    /// <summary>
    /// Registers an in-memory <see cref="IDatabase"/> as a singleton.
    /// Data is not persisted across application restarts; this is primarily useful for testing.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="name">The database name. Defaults to <c>"default"</c>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// <see cref="DatabaseFactory.CreateInMemoryDatabaseAsync"/> is asynchronous; this method blocks via
    /// <c>.GetAwaiter().GetResult()</c> to satisfy the synchronous DI registration contract.
    /// </remarks>
    public static IServiceCollection AddPersistXInMemoryDatabase(
        this IServiceCollection services,
        string name = "default")
    {
        services.AddSingleton<IDatabase>(sp =>
            DatabaseFactory.CreateInMemoryDatabaseAsync(name, sp)
                .GetAwaiter()
                .GetResult());

        return services;
    }

    /// <summary>
    /// Registers a SQLite-backed <see cref="IDatabase"/> as a singleton.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="name">The database name.</param>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// <see cref="DatabaseFactory.CreateAsync"/> is asynchronous; this method blocks via
    /// <c>.GetAwaiter().GetResult()</c> to satisfy the synchronous DI registration contract.
    /// </remarks>
    public static IServiceCollection AddPersistXSQLiteDatabase(
        this IServiceCollection services,
        string name,
        string connectionString)
    {
        services.AddSingleton<IDatabase>(sp =>
            DatabaseFactory.CreateAsync(
                name,
                new DatabaseConfiguration
                {
                    BackendType = BackendType.SQLite,
                    BackendConfiguration = new Dictionary<string, string>
                    {
                        ["ConnectionString"] = connectionString
                    }
                },
                sp)
                .GetAwaiter()
                .GetResult());

        return services;
    }
}
