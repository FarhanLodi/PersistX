using PersistX.Interfaces;

namespace PersistX.Database;

/// <summary>
/// Backend configuration implementation for database initialization.
/// </summary>
internal class DatabaseBackendConfiguration : IBackendConfiguration, IWriteAheadLogConfiguration, ICompressionProviderConfiguration, IEncryptionProviderConfiguration, IBackupProviderConfiguration
{
    private readonly Dictionary<string, string> _configuration = new();

    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration value, or null if not found</returns>
    public string? GetValue(string key)
    {
        return _configuration.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Gets a configuration value by key with a default value.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if key not found</param>
    /// <returns>Configuration value or default</returns>
    public string GetValue(string key, string defaultValue)
    {
        return _configuration.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Gets all configuration keys.
    /// </summary>
    /// <returns>Collection of configuration keys</returns>
    public IEnumerable<string> GetKeys()
    {
        return _configuration.Keys;
    }

    /// <summary>
    /// Sets a configuration value by key.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Configuration value</param>
    public void SetValue(string key, string value)
    {
        _configuration[key] = value;
    }
}
