using PersistX.Enums;

namespace PersistX.Models;

/// <summary>
/// Comprehensive database statistics including advanced storage features.
/// </summary>
public record ComprehensiveDatabaseStatistics : DatabaseStatistics
{
    /// <summary>
    /// Whether Write-Ahead Logging is enabled.
    /// </summary>
    public bool HasWriteAheadLog { get; init; }

    /// <summary>
    /// Whether compression is enabled.
    /// </summary>
    public bool HasCompression { get; init; }

    /// <summary>
    /// Whether encryption is enabled.
    /// </summary>
    public bool HasEncryption { get; init; }

    /// <summary>
    /// Whether backup is enabled.
    /// </summary>
    public bool HasBackup { get; init; }

    /// <summary>
    /// Type of compression being used.
    /// </summary>
    public CompressionType? CompressionType { get; init; }

    /// <summary>
    /// Type of encryption being used.
    /// </summary>
    public EncryptionType? EncryptionType { get; init; }

    /// <summary>
    /// Size of the Write-Ahead Log in bytes.
    /// </summary>
    public long WalSizeBytes { get; init; }
}
