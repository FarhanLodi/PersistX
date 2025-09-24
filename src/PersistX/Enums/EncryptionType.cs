namespace PersistX.Enums;

/// <summary>
/// Supported encryption types for data encryption at rest.
/// </summary>
public enum EncryptionType
{
    /// <summary>
    /// No encryption - data is stored in plain text.
    /// Use only for non-sensitive data or when encryption is handled externally.
    /// </summary>
    None = 0,

    /// <summary>
    /// AES (Advanced Encryption Standard) encryption.
    /// Provides strong encryption with AES-256-GCM for authenticated encryption.
    /// Best for sensitive data that requires strong security.
    /// </summary>
    Aes = 1
}
