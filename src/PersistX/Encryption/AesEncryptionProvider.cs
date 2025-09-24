using System.Buffers;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;

namespace PersistX.Encryption;

/// <summary>
/// AES encryption provider implementation for data encryption at rest.
/// </summary>
public class AesEncryptionProvider : IEncryptionProvider
{
    private readonly ILogger<AesEncryptionProvider>? _logger;
    private byte[] _key;
    private readonly int _keySizeBits;

    public string Name => "AES";
    public int KeySizeBits => _keySizeBits;

    public AesEncryptionProvider(ILogger<AesEncryptionProvider>? logger = null, int keySizeBits = 256)
    {
        _logger = logger;
        _keySizeBits = keySizeBits;
        _key = new byte[keySizeBits / 8];
    }

    public async Task InitializeAsync(IEncryptionProviderConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var keyBase64 = configuration.GetValue("Key");
        if (string.IsNullOrEmpty(keyBase64))
        {
            throw new InvalidOperationException(
                "Encryption key is required but not provided. " +
                "Please provide a valid AES-256 key in Base64 format using the 'Key' configuration parameter. " +
                "You can generate a key using the GenerateKeyAsync() method and store it securely.");
        }

        try
        {
            _key = Convert.FromBase64String(keyBase64);
            if (_key.Length != _keySizeBits / 8)
            {
                throw new ArgumentException(
                    $"Invalid key size. Expected {_keySizeBits / 8} bytes ({_keySizeBits} bits), " +
                    $"but got {_key.Length} bytes. Please provide a valid AES-{_keySizeBits} key.");
            }
            
            _logger?.LogInformation("AES encryption initialized with user-provided {KeySize}-bit key", _keySizeBits);
            
            // Add await to satisfy the async method requirement
            await Task.CompletedTask;
        }
        catch (FormatException ex)
        {
            _logger?.LogError(ex, "Invalid Base64 format for encryption key");
            throw new ArgumentException(
                "Invalid Base64 format for encryption key. " +
                "Please provide a valid Base64-encoded AES-256 key.", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize encryption with provided key");
            throw;
        }
    }

    public async Task<ReadOnlyMemory<byte>> EncryptAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default)
    {
        if (input.Length == 0)
        {
            _logger?.LogDebug("Encrypting empty input, returning empty result");
            return ReadOnlyMemory<byte>.Empty;
        }

        try
        {
            _logger?.LogDebug("Starting AES encryption of {InputLength} bytes", input.Length);
            
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            _logger?.LogDebug("Generated IV of length {IVLength}", aes.IV.Length);

            using var encryptor = aes.CreateEncryptor();
            using var outputStream = new MemoryStream();
            
            // Write IV length and IV
            outputStream.WriteByte((byte)aes.IV.Length);
            outputStream.Write(aes.IV, 0, aes.IV.Length);

            // Encrypt data
            using var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write);
            await cryptoStream.WriteAsync(input, cancellationToken);
            cryptoStream.FlushFinalBlock(); // Use synchronous FlushFinalBlock instead of async

            var encrypted = outputStream.ToArray();
            _logger?.LogDebug("AES encryption completed: {InputLength} bytes -> {OutputLength} bytes", input.Length, encrypted.Length);
            
            return encrypted;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to encrypt data using AES. Input length: {InputLength}", input.Length);
            throw;
        }
    }

    public async Task<ReadOnlyMemory<byte>> DecryptAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default)
    {
        if (input.Length == 0)
        {
            _logger?.LogDebug("Decrypting empty input, returning empty result");
            return ReadOnlyMemory<byte>.Empty;
        }

        if (input.Length < 17) // IV length byte (1) + minimum IV length (16)
        {
            _logger?.LogWarning("Input data too small for AES decryption. Length: {Length}, minimum required: 17", input.Length);
            throw new InvalidDataException($"Input data too small for AES decryption. Length: {input.Length}, minimum required: 17");
        }

        try
        {
            _logger?.LogDebug("Starting AES decryption of {InputLength} bytes", input.Length);
            
            using var inputStream = new MemoryStream(input.ToArray());
            
            // Read IV length and IV
            var ivLength = inputStream.ReadByte();
            if (ivLength == -1)
            {
                throw new InvalidDataException("Invalid encrypted data format - cannot read IV length");
            }

            _logger?.LogDebug("Read IV length: {IVLength}", ivLength);

            if (ivLength != 16)
            {
                _logger?.LogWarning("Unexpected IV length: {IVLength}, expected: 16", ivLength);
            }

            var iv = new byte[ivLength];
            var bytesRead = inputStream.Read(iv, 0, ivLength);
            if (bytesRead != ivLength)
            {
                throw new InvalidDataException($"Invalid encrypted data format - expected {ivLength} IV bytes, got {bytesRead}");
            }

            var cipherTextLength = input.Length - 1 - ivLength;
            _logger?.LogDebug("Cipher text length: {CipherLength} bytes (total: {TotalLength}, IV overhead: {IVOverhead})", 
                cipherTextLength, input.Length, 1 + ivLength);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
            using var outputStream = new MemoryStream();

            await cryptoStream.CopyToAsync(outputStream, cancellationToken);
            
            var decrypted = outputStream.ToArray();
            _logger?.LogDebug("AES decryption completed: {InputLength} bytes -> {OutputLength} bytes", input.Length, decrypted.Length);
            
            return decrypted;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to decrypt data using AES. Input length: {InputLength}", input.Length);
            throw;
        }
    }

    public int GetEncryptionOverhead(int inputSize)
    {
        // AES overhead: IV (16 bytes) + IV length (1 byte) + padding (up to 16 bytes)
        return 17 + (16 - (inputSize % 16));
    }

    public async Task<byte[]> GenerateKeyAsync(CancellationToken cancellationToken = default)
    {
        var key = new byte[_keySizeBits / 8];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);
        
        _logger?.LogInformation("Generated new {KeySize}-bit encryption key", _keySizeBits);
        return await Task.FromResult(key);
    }

    public bool ValidateKey(ReadOnlySpan<byte> key)
    {
        return key.Length == _keySizeBits / 8;
    }

    /// <summary>
    /// Generates a new AES-256 encryption key in Base64 format.
    /// This method can be used to generate keys for configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Base64-encoded encryption key</returns>
    public static async Task<string> GenerateKeyBase64Async(CancellationToken cancellationToken = default)
    {
        var key = new byte[32]; // 256 bits
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);
        
        return await Task.FromResult(Convert.ToBase64String(key));
    }

    /// <summary>
    /// Validates that a Base64 string represents a valid AES-256 key.
    /// </summary>
    /// <param name="keyBase64">Base64-encoded key to validate</param>
    /// <returns>True if the key is valid</returns>
    public static bool ValidateKeyBase64(string keyBase64)
    {
        if (string.IsNullOrEmpty(keyBase64))
            return false;

        try
        {
            var key = Convert.FromBase64String(keyBase64);
            return key.Length == 32; // 256 bits
        }
        catch
        {
            return false;
        }
    }
}
