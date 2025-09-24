namespace PersistX.Enums;

/// <summary>
/// Supported compression types for data compression.
/// </summary>
public enum CompressionType
{
    /// <summary>
    /// No compression - data is stored as-is for maximum performance.
    /// </summary>
    None = 0,

    /// <summary>
    /// GZip compression - good balance of compression ratio and performance.
    /// Best for general-purpose compression with good compatibility.
    /// </summary>
    GZip = 1,

    /// <summary>
    /// Deflate compression - maximum compression efficiency with lower overhead.
    /// Best for when you need maximum compression with minimal overhead.
    /// </summary>
    Deflate = 2
}
