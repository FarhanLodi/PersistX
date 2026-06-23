using PersistX.Interfaces;

namespace PersistX.UnitTests;

/// <summary>Minimal IBackendConfiguration for tests.</summary>
internal sealed class TestBackendConfig : IBackendConfiguration
{
    private readonly Dictionary<string, string> _values;

    public TestBackendConfig(Dictionary<string, string>? values = null)
        => _values = values ?? new Dictionary<string, string>();

    public string? GetValue(string key) => _values.TryGetValue(key, out var v) ? v : null;
    public string GetValue(string key, string defaultValue) => _values.TryGetValue(key, out var v) ? v : defaultValue;
    public IEnumerable<string> GetKeys() => _values.Keys;
}
