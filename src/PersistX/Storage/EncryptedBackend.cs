using System.Buffers;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;

namespace PersistX.Storage;

/// <summary>
/// Backend wrapper that adds encryption/decryption to any underlying backend.
/// Always operates in appendable mode for encrypted data.
/// </summary>
public class EncryptedBackend : IBackend
{
    private readonly IBackend _underlyingBackend;
    private readonly IEncryptionProvider _encryptionProvider;
    private readonly ILogger<EncryptedBackend>? _logger;

    public string Name => $"Encrypted_{_underlyingBackend.Name}";

    public EncryptedBackend(IBackend underlyingBackend, IEncryptionProvider encryptionProvider, ILogger<EncryptedBackend>? logger = null)
    {
        _underlyingBackend = underlyingBackend ?? throw new ArgumentNullException(nameof(underlyingBackend));
        _encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
        _logger = logger;
    }

    public async Task InitializeAsync(IBackendConfiguration configuration, CancellationToken cancellationToken = default)
    {
        // Do not re-initialize the underlying backend here.
        // The database initializes the underlying backend before wrapping it with EncryptedBackend.
        // Re-initializing can cause resource conflicts (e.g., WAL file opened twice).
        await Task.CompletedTask;
        _logger?.LogInformation("EncryptedBackend ready with encryption provider: {EncryptionProvider}", _encryptionProvider.Name);
    }

    public async Task<ReadOnlyMemory<byte>> ReadAsync(string location, long offset, int length, CancellationToken cancellationToken = default)
    {
        // Read encrypted data from underlying backend
        var encryptedData = await _underlyingBackend.ReadAsync(location, offset, length, cancellationToken);
        
        if (encryptedData.Length == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

                try
                {
                    // Decrypt the data
                    var decryptedData = await _encryptionProvider.DecryptAsync(encryptedData, cancellationToken);
                    
                    _logger?.LogDebug("Decrypted {EncryptedSize} bytes to {DecryptedSize} bytes for location {Location}", 
                        encryptedData.Length, decryptedData.Length, location);
                    
                    // Log first few bytes of decrypted data for debugging
                    if (decryptedData.Length > 0)
                    {
                        var preview = decryptedData.Span.Slice(0, Math.Min(50, decryptedData.Length));
                        var hex = Convert.ToHexString(preview);
                        _logger?.LogDebug("Decrypted data preview (hex): {Hex}", hex);
                        
                        // Try to decode as UTF-8 to see if it's readable
                        try
                        {
                            var text = System.Text.Encoding.UTF8.GetString(preview);
                            _logger?.LogDebug("Decrypted data preview (text): {Text}", text);
                        }
                        catch
                        {
                            _logger?.LogDebug("Decrypted data preview (text): [Not valid UTF-8]");
                        }
                    }
                    
                    return decryptedData;
                }
                catch (Exception ex) when (ex is CryptographicException || ex is InvalidDataException)
                {
                    _logger?.LogError(ex, "Failed to decrypt data for location {Location}. Encrypted size: {EncryptedSize} bytes", location, encryptedData.Length);
                    throw; // Re-throw instead of returning empty data
                }
    }

    public async Task WriteAsync(string location, long offset, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (data.Length == 0)
        {
            await _underlyingBackend.WriteAsync(location, offset, data, cancellationToken);
            return;
        }

        // Encrypt the data before writing
        var encryptedData = await _encryptionProvider.EncryptAsync(data, cancellationToken);
        
        _logger?.LogDebug("Encrypted {OriginalSize} bytes to {EncryptedSize} bytes for location {Location}", 
            data.Length, encryptedData.Length, location);
        
        // Always operate in appendable mode for encrypted data
        if (offset == 0)
        {
            // If offset is 0, we're starting fresh - write the encrypted data
            await _underlyingBackend.WriteAsync(location, 0, encryptedData, cancellationToken);
        }
        else
        {
            // If offset is not 0, we're appending - get the current file size and append
            var currentSize = await _underlyingBackend.GetSizeAsync(location, cancellationToken);
            if (currentSize < 0)
            {
                // File doesn't exist, write from beginning
                await _underlyingBackend.WriteAsync(location, 0, encryptedData, cancellationToken);
            }
            else
            {
                // Append to the end of the file
                await _underlyingBackend.WriteAsync(location, currentSize, encryptedData, cancellationToken);
            }
        }
    }

    public async Task DeleteAsync(string location, CancellationToken cancellationToken = default)
    {
        await _underlyingBackend.DeleteAsync(location, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string location, CancellationToken cancellationToken = default)
    {
        return await _underlyingBackend.ExistsAsync(location, cancellationToken);
    }

    public async Task<long> GetSizeAsync(string location, CancellationToken cancellationToken = default)
    {
        return await _underlyingBackend.GetSizeAsync(location, cancellationToken);
    }

    public async IAsyncEnumerable<string> ListLocationsAsync(string? pattern = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var location in _underlyingBackend.ListLocationsAsync(pattern, cancellationToken))
        {
            yield return location;
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await _underlyingBackend.FlushAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _underlyingBackend.DisposeAsync();
    }
}
