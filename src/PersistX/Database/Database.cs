using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PersistX.Collections;
using PersistX.Interfaces;
using PersistX.Models;
using PersistX.Storage;
using PersistX.Compression;
using PersistX.Encryption;
using PersistX.Backup;
using PersistX.Enums;

namespace PersistX.Database;

/// <summary>
/// Main database implementation for PersistX.
/// </summary>
public class Database : IDatabase
{
    private readonly ILogger<Database>? _logger;
    private readonly ConcurrentDictionary<string, object> _collections = new();
    private readonly TransactionManager _transactionManager;
    private readonly DatabaseConfiguration _configuration;
    private bool _disposed;
    private bool _initialized;
    
    // Advanced Storage Features (optional)
    private ICompressionProvider? _compressionProvider;
    private IEncryptionProvider? _encryptionProvider;
    private IBackupProvider? _backupProvider;

    public string Name { get; }
    public IBackend Backend { get; private set; }
    public ITransactionManager TransactionManager => _transactionManager;

    public Database(string name, IBackend backend, DatabaseConfiguration configuration, ILogger<Database>? logger = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
        _transactionManager = new TransactionManager(logger as ILogger<TransactionManager>);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        try
        {
            var backendConfig = new DatabaseBackendConfiguration();
            if (_configuration.BackendConfiguration != null)
            {
                foreach (var kvp in _configuration.BackendConfiguration)
                {
                    backendConfig.SetValue(kvp.Key, kvp.Value);
                }
            }
            
            // Initialize the original backend first
            await Backend.InitializeAsync(backendConfig, cancellationToken);
            
            // Initialize advanced storage features if configured
            await InitializeAdvancedStorageFeaturesAsync(cancellationToken);
            
            // Wrap backend with encryption if encryption is enabled
            if (_encryptionProvider != null)
            {
                var encryptedBackend = new EncryptedBackend(Backend, _encryptionProvider, _logger as ILogger<EncryptedBackend>);
                await encryptedBackend.InitializeAsync(backendConfig, cancellationToken);
                Backend = encryptedBackend;
                _logger?.LogInformation("Backend wrapped with appendable encryption for database '{Name}'", Name);
            }
            
            _initialized = true;
            _logger?.LogInformation("Database '{Name}' initialized successfully", Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize database '{Name}'", Name);
            throw;
        }
    }

    public async Task<IPersistentCollection<T>> CreateCollectionAsync<T>(
        string name,
        ISerializer<List<T>>? serializer = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name cannot be null or empty", nameof(name));

        if (_collections.ContainsKey(name))
            throw new InvalidOperationException($"Collection '{name}' already exists");

        try
        {
            // Create a persistent collection with the specified backend and serializer
            var collection = new PersistentCollection<T>(name, Backend, serializer, _logger as ILogger<PersistentCollection<T>>);
            await collection.InitializeAsync(cancellationToken);

            _collections.TryAdd(name, collection);
            _logger?.LogInformation("Created collection '{Name}' in database '{DatabaseName}'", name, Name);

            return collection;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create collection '{Name}' in database '{DatabaseName}'", name, Name);
            throw;
        }
    }

    public async Task<IPersistentCollection<T>?> GetCollectionAsync<T>(string name, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        if (string.IsNullOrWhiteSpace(name))
            return null;

        if (_collections.TryGetValue(name, out var collection) && collection is IPersistentCollection<T> typedCollection)
        {
            return await Task.FromResult(typedCollection);
        }

        // Try to load from storage
        try
        {
            var dataLocation = $"{name}.data";
            if (await Backend.ExistsAsync(dataLocation, cancellationToken))
            {
                var loadedCollection = new PersistentCollection<T>(name, Backend, null, _logger as ILogger<PersistentCollection<T>>);
                await loadedCollection.InitializeAsync(cancellationToken);
                
                _collections.TryAdd(name, loadedCollection);
                return loadedCollection;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load collection '{Name}' from storage", name);
        }

        return null;
    }

    public async Task DropCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            // Remove from memory
            if (_collections.TryRemove(name, out var collection) && collection is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            // Remove from storage
            var dataLocation = $"{name}.data";
            var metadataLocation = $"{name}.metadata";
            
            if (await Backend.ExistsAsync(dataLocation, cancellationToken))
            {
                await Backend.DeleteAsync(dataLocation, cancellationToken);
            }
            
            if (await Backend.ExistsAsync(metadataLocation, cancellationToken))
            {
                await Backend.DeleteAsync(metadataLocation, cancellationToken);
            }

            _logger?.LogInformation("Dropped collection '{Name}' from database '{DatabaseName}'", name, Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to drop collection '{Name}' from database '{DatabaseName}'", name, Name);
            throw;
        }
    }

    public async IAsyncEnumerable<string> GetCollectionNamesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        // Return collections in memory
        foreach (var name in _collections.Keys)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            yield return name;
            await Task.Yield();
        }

        // Also check for collections in storage that might not be loaded
        await foreach (var location in Backend.ListLocationsAsync("*.data", cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            var name = location.Replace(".data", "");
            if (!string.IsNullOrEmpty(name) && !_collections.ContainsKey(name))
            {
                yield return name;
            }
            
            await Task.Yield();
        }
    }

    public async Task<ITransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        return await _transactionManager.BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<ITransaction, Task<TResult>> func,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            var result = await func(transaction);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ExecuteInTransactionAsync(
        Func<ITransaction, Task> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            await action(transaction);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        try
        {
            await Backend.FlushAsync(cancellationToken);
            _logger?.LogDebug("Database '{Name}' flushed successfully", Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to flush database '{Name}'", Name);
            throw;
        }
    }

    public async Task<DatabaseStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        var statistics = new DatabaseStatistics
        {
            CollectionCount = _collections.Count,
            ActiveTransactionCount = _transactionManager.GetActiveTransactions().Count,
            CreatedAt = DateTime.UtcNow, // This should be stored and retrieved from metadata
            LastMaintenance = DateTime.UtcNow // This should be stored and retrieved from metadata
        };

        // Calculate total storage size
        long totalSize = 0;
        await foreach (var location in Backend.ListLocationsAsync(cancellationToken: cancellationToken))
        {
            var size = await Backend.GetSizeAsync(location, cancellationToken);
            if (size > 0)
            {
                totalSize += size;
            }
        }

        statistics.TotalStorageSize = totalSize;

        return await Task.FromResult(statistics);
    }

    public async Task MaintenanceAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        try
        {
            _logger?.LogInformation("Starting maintenance for database '{Name}'", Name);

            // Rebuild indexes for all collections
            foreach (var collection in _collections.Values.OfType<IPersistentCollection<object>>())
            {
                await collection.RebuildIndexesAsync(cancellationToken);
            }

            // Flush all pending changes
            await FlushAsync(cancellationToken);

            _logger?.LogInformation("Completed maintenance for database '{Name}'", Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to perform maintenance for database '{Name}'", Name);
            throw;
        }
    }

    // Advanced Storage Features Methods

    /// <summary>
    /// Creates a backup of the database.
    /// </summary>
    /// <param name="backupId">Unique identifier for the backup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backup metadata</returns>
    public async Task<BackupMetadata> CreateBackupAsync(string backupId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        if (_backupProvider == null)
        {
            throw new InvalidOperationException("Backup provider is not configured. Enable backup in database configuration.");
        }

        // Flush all pending data to ensure files are properly written and closed
        await FlushAsync(cancellationToken);

        return await _backupProvider.CreateBackupAsync(Backend, backupId, cancellationToken);
    }

    /// <summary>
    /// Restores the database from a backup.
    /// </summary>
    /// <param name="backupId">Backup identifier to restore from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task RestoreBackupAsync(string backupId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        if (_backupProvider == null)
        {
            throw new InvalidOperationException("Backup provider is not configured. Enable backup in database configuration.");
        }

        await _backupProvider.RestoreBackupAsync(Backend, backupId, cancellationToken);
        
        // Clear collections cache to force reload from restored storage
        await ClearCollectionsCacheAsync(cancellationToken);
    }

    /// <summary>
    /// Lists all available backups.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of backup metadata</returns>
    public async IAsyncEnumerable<BackupMetadata> ListBackupsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        if (_backupProvider == null)
        {
            yield break;
        }

        await foreach (var backup in _backupProvider.ListBackupsAsync(cancellationToken))
        {
            yield return backup;
        }
    }

    /// <summary>
    /// Gets comprehensive statistics about the database including advanced storage features.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comprehensive database statistics</returns>
    public async Task<ComprehensiveDatabaseStatistics> GetComprehensiveStatisticsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        var baseStats = await GetStatisticsAsync(cancellationToken);
        
        // Get WAL size if available (use FileStorage's WAL)
        long walSizeBytes = 0;
        bool hasWriteAheadLog = false;
        
        if (Backend is FileStorage fileStorage)
        {
            walSizeBytes = await fileStorage.GetWalSizeAsync(cancellationToken);
            hasWriteAheadLog = walSizeBytes >= 0; // WAL exists if size is >= 0
        }
        
        var comprehensiveStats = new ComprehensiveDatabaseStatistics
        {
            CollectionCount = baseStats.CollectionCount,
            ActiveTransactionCount = baseStats.ActiveTransactionCount,
            TotalStorageSize = baseStats.TotalStorageSize,
            CreatedAt = baseStats.CreatedAt,
            LastMaintenance = baseStats.LastMaintenance,
            
            // Advanced storage features
            HasWriteAheadLog = hasWriteAheadLog,
            HasCompression = _compressionProvider != null,
            HasEncryption = _encryptionProvider != null,
            HasBackup = _backupProvider != null,
            CompressionType = _compressionProvider?.Name switch
            {
                "GZip" => Enums.CompressionType.GZip,
                "Deflate" => Enums.CompressionType.Deflate,
                _ => null
            },
            EncryptionType = _encryptionProvider?.Name switch
            {
                "AES" => Enums.EncryptionType.Aes,
                _ => null
            },
            WalSizeBytes = walSizeBytes
        };

        return comprehensiveStats;
    }

    /// <summary>
    /// Clears the collections cache, forcing collections to be reloaded from storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ClearCollectionsCacheAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        try
        {
            // Dispose all cached collections
            foreach (var collection in _collections.Values.OfType<IAsyncDisposable>())
            {
                try
                {
                    await collection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error disposing collection during cache clear");
                }
            }

            // Clear the cache
            _collections.Clear();

            _logger?.LogInformation("Cleared collections cache for database '{Name}'", Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clear collections cache for database '{Name}'", Name);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // Dispose all collections
            foreach (var collection in _collections.Values.OfType<IAsyncDisposable>())
            {
                await collection.DisposeAsync();
            }
            _collections.Clear();

            // Dispose transaction manager
            await _transactionManager.DisposeAsync();

            // Dispose advanced storage features
            if (_backupProvider != null)
            {
                await _backupProvider.DisposeAsync();
            }

            // Dispose backend
            await Backend.DisposeAsync();

            _logger?.LogInformation("Database '{Name}' disposed successfully", Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disposing database '{Name}'", Name);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Database));
        }
    }

    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Database has not been initialized. Call InitializeAsync() first.");
        }
    }

    private async Task InitializeAdvancedStorageFeaturesAsync(CancellationToken cancellationToken)
    {
        // Initialize Compression Provider if enabled
        var compressionTypeStr = _configuration.BackendConfiguration?.GetValueOrDefault("CompressionType", "None");
        if (Enum.TryParse<CompressionType>(compressionTypeStr, true, out var compressionType) && compressionType != CompressionType.None)
        {
            _compressionProvider = compressionType switch
            {
                CompressionType.GZip => new GZipCompressionProvider(_logger as ILogger<GZipCompressionProvider>),
                CompressionType.Deflate => new DeflateCompressionProvider(_logger as ILogger<DeflateCompressionProvider>),
                _ => throw new NotSupportedException($"Compression type '{compressionType}' is not supported")
            };
            _logger?.LogInformation("Compression enabled ({CompressionType}) for database '{Name}'", compressionType, Name);
        }

        // Initialize Encryption Provider if enabled
        var encryptionTypeStr = _configuration.BackendConfiguration?.GetValueOrDefault("EncryptionType", "None");
        if (Enum.TryParse<EncryptionType>(encryptionTypeStr, true, out var encryptionType) && encryptionType != EncryptionType.None)
        {
            _encryptionProvider = encryptionType switch
            {
                EncryptionType.Aes => new AesEncryptionProvider(_logger as ILogger<AesEncryptionProvider>),
                _ => throw new NotSupportedException($"Encryption type '{encryptionType}' is not supported")
            };

            var encryptionConfig = new DatabaseBackendConfiguration();
            var encryptionKey = _configuration.BackendConfiguration?.GetValueOrDefault("EncryptionKey");
            if (!string.IsNullOrEmpty(encryptionKey))
            {
                encryptionConfig.SetValue("Key", encryptionKey);
            }
            await _encryptionProvider.InitializeAsync(encryptionConfig, cancellationToken);
            _logger?.LogInformation("Encryption enabled ({EncryptionType}) for database '{Name}'", encryptionType, Name);
        }

        // Initialize Backup Provider if enabled
        var enableBackup = _configuration.BackendConfiguration?.GetValueOrDefault("EnableBackup", "false").ToLowerInvariant() == "true";
        if (enableBackup)
        {
            _backupProvider = new FileBackupProvider(_logger as ILogger<FileBackupProvider>);
            var backupConfig = new DatabaseBackendConfiguration();
            backupConfig.SetValue("BackupPath", Path.Combine(Environment.CurrentDirectory, "persistx_backups", Name));
            await _backupProvider.InitializeAsync(backupConfig, cancellationToken);
            _logger?.LogInformation("Backup enabled for database '{Name}'", Name);
        }
    }
}

