using System.Buffers;

namespace PersistX.Interfaces;

/// <summary>
/// Represents an encryption provider for data encryption and decryption at rest.
/// </summary>
public interface IEncryptionProvider
{
    /// <summary>
    /// Gets the name of this encryption algorithm.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the key size in bits.
    /// </summary>
    int KeySizeBits { get; }

    /// <summary>
    /// Initializes the encryption provider with the specified configuration.
    /// </summary>
    /// <param name="configuration">Encryption configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(IEncryptionProviderConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts the input data.
    /// </summary>
    /// <param name="input">Input data to encrypt</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Encrypted data</returns>
    Task<ReadOnlyMemory<byte>> EncryptAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts the input data.
    /// </summary>
    /// <param name="input">Encrypted data to decrypt</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Decrypted data</returns>
    Task<ReadOnlyMemory<byte>> DecryptAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the overhead in bytes for encryption (IV, padding, etc.).
    /// </summary>
    /// <param name="inputSize">Size of input data in bytes</param>
    /// <returns>Encryption overhead in bytes</returns>
    int GetEncryptionOverhead(int inputSize);

    /// <summary>
    /// Generates a new encryption key.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New encryption key</returns>
    Task<byte[]> GenerateKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the provided key is valid for this encryption provider.
    /// </summary>
    /// <param name="key">Key to validate</param>
    /// <returns>True if the key is valid</returns>
    bool ValidateKey(ReadOnlySpan<byte> key);
}

/// <summary>
/// Configuration interface for encryption providers.
/// </summary>
public interface IEncryptionProviderConfiguration
{
    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration value, or null if not found</returns>
    string? GetValue(string key);

    /// <summary>
    /// Gets a configuration value by key with a default value.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if key not found</param>
    /// <returns>Configuration value or default</returns>
    string GetValue(string key, string defaultValue);

    /// <summary>
    /// Gets all configuration keys.
    /// </summary>
    /// <returns>Collection of configuration keys</returns>
    IEnumerable<string> GetKeys();
}
