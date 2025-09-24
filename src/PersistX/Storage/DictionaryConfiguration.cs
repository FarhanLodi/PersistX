using PersistX.Interfaces;

namespace PersistX.Storage;

/// <summary>
/// Dictionary-based configuration implementation that implements multiple configuration interfaces.
/// </summary>
internal class DictionaryConfiguration : IBackendConfiguration, IWriteAheadLogConfiguration, ICompressionProviderConfiguration, IEncryptionProviderConfiguration, IBackupProviderConfiguration
{
    private readonly Dictionary<string, string> _values = new();

    /// <summary>
    /// Gets or sets a configuration value by key.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration value</returns>
    public string this[string key]
    {
        get => _values[key];
        set => _values[key] = value;
    }

    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration value, or null if not found</returns>
    public string? GetValue(string key)
    {
        return _values.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Gets a configuration value by key with a default value.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if key not found</param>
    /// <returns>Configuration value or default</returns>
    public string GetValue(string key, string defaultValue)
    {
        return _values.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Gets all configuration keys.
    /// </summary>
    /// <returns>Collection of configuration keys</returns>
    public IEnumerable<string> GetKeys()
    {
        return _values.Keys;
    }
}
