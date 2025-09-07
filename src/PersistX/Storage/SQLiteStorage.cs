using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;

namespace PersistX.Storage;

/// <summary>
/// SQLite-based storage for PersistX.
/// </summary>
public class SQLiteStorage : IBackend
{
    private readonly ILogger<SQLiteStorage>? _logger;
    private string _connectionString = string.Empty;
    private bool _disposed;

    public string Name => "SQLiteStorage";

    public SQLiteStorage(ILogger<SQLiteStorage>? logger = null)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(IBackendConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var dataSource = configuration.GetValue("DataSource", "persistx.db");
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = dataSource,
            Cache = SqliteCacheMode.Shared
        };

        _connectionString = connectionStringBuilder.ConnectionString;

        // Initialize the database schema
        await InitializeSchemaAsync(cancellationToken);

        _logger?.LogInformation("SQLiteStorage initialized with data source: {DataSource}", dataSource);
    }

    public async Task<ReadOnlyMemory<byte>> ReadAsync(string location, long offset, int length, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT data FROM storage 
            WHERE location = @location 
            AND @offset >= start_offset 
            AND @offset + @length <= start_offset + data_length
            ORDER BY start_offset";

        command.Parameters.AddWithValue("@location", location);
        command.Parameters.AddWithValue("@offset", (int)offset);
        command.Parameters.AddWithValue("@length", length);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new KeyNotFoundException($"Data not found at location {location}, offset {offset}");
        }

        var data = (byte[])reader["data"];
        var relativeOffset = (int)(offset - GetStartOffset(location, offset));
        var actualLength = Math.Min(length, data.Length - relativeOffset);
        
        var result = new byte[actualLength];
        Array.Copy(data, relativeOffset, result, 0, actualLength);

        return new ReadOnlyMemory<byte>(result);
    }

    public async Task WriteAsync(string location, long offset, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();

        try
        {
            // Check if we need to update existing data or insert new
            var existingCommand = connection.CreateCommand();
            existingCommand.CommandText = @"
                SELECT id, start_offset, data_length, data 
                FROM storage 
                WHERE location = @location 
                AND start_offset <= @offset 
                AND start_offset + data_length >= @offset + @length";

            existingCommand.Parameters.AddWithValue("@location", location);
            existingCommand.Parameters.AddWithValue("@offset", (int)offset);
            existingCommand.Parameters.AddWithValue("@length", data.Length);

            using var reader = await existingCommand.ExecuteReaderAsync(cancellationToken);
            
            if (await reader.ReadAsync(cancellationToken))
            {
                // Update existing record
                var id = reader.GetInt64(reader.GetOrdinal("id"));
                var startOffset = reader.GetInt64(reader.GetOrdinal("start_offset"));
                var dataLength = reader.GetInt64(reader.GetOrdinal("data_length"));
                var existingData = (byte[])reader[reader.GetOrdinal("data")];

                var newData = new byte[Math.Max(existingData.Length, (int)(offset - startOffset + data.Length))];
                Array.Copy(existingData, newData, existingData.Length);
                data.CopyTo(newData.AsMemory((int)(offset - startOffset)));

                var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = @"
                    UPDATE storage 
                    SET data = @data, data_length = @data_length 
                    WHERE id = @id";

                updateCommand.Parameters.AddWithValue("@data", newData);
                updateCommand.Parameters.AddWithValue("@data_length", newData.Length);
                updateCommand.Parameters.AddWithValue("@id", id);

                await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                // Insert new record
                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = @"
                    INSERT INTO storage (location, start_offset, data_length, data) 
                    VALUES (@location, @start_offset, @data_length, @data)";

                insertCommand.Parameters.AddWithValue("@location", location);
                insertCommand.Parameters.AddWithValue("@start_offset", (int)offset);
                insertCommand.Parameters.AddWithValue("@data_length", data.Length);
                insertCommand.Parameters.AddWithValue("@data", data.ToArray());

                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteAsync(string location, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM storage WHERE location = @location";
        command.Parameters.AddWithValue("@location", location);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string location, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM storage WHERE location = @location";
        command.Parameters.AddWithValue("@location", location);

        var count = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(count) > 0;
    }

    public async Task<long> GetSizeAsync(string location, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(start_offset + data_length) - MIN(start_offset), 0) FROM storage WHERE location = @location";
        command.Parameters.AddWithValue("@location", location);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result == DBNull.Value ? -1 : Convert.ToInt64(result);
    }

    public async IAsyncEnumerable<string> ListLocationsAsync(string? pattern = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT location FROM storage";
        
        if (!string.IsNullOrEmpty(pattern))
        {
            command.CommandText += " WHERE location LIKE @pattern";
            command.Parameters.AddWithValue("@pattern", pattern.Replace("*", "%"));
        }

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        while (await reader.ReadAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            yield return reader.GetString(reader.GetOrdinal("location"));
            await Task.Yield(); // Allow cancellation
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await Task.CompletedTask;
    }

    private async Task InitializeSchemaAsync(CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS storage (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                location TEXT NOT NULL,
                start_offset INTEGER NOT NULL,
                data_length INTEGER NOT NULL,
                data BLOB NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS idx_storage_location ON storage(location);
            CREATE INDEX IF NOT EXISTS idx_storage_offset ON storage(location, start_offset);";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private long GetStartOffset(string location, long offset)
    {
        // This is a simplified implementation
        // In a real implementation, you'd query the database to find the actual start offset
        return 0;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SQLiteStorage));
        }
    }
}
