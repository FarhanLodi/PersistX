using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PersistX.Backup;

/// <summary>
/// Creates and restores database snapshots as ZIP archives (.snap files).
/// Also provides export utilities for collections.
/// </summary>
public sealed class SnapshotManager
{
    private const string ManifestEntryName = "manifest.json";

    private readonly ILogger<SnapshotManager>? _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SnapshotManager(ILogger<SnapshotManager>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create a snapshot ZIP of all files in <paramref name="sourceDirectory"/>.
    /// Saves to <paramref name="snapshotPath"/> (should end with .snap).
    /// </summary>
    public async Task CreateSnapshotAsync(
        string sourceDirectory,
        string snapshotPath,
        string databaseName = "",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath);

        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

        // Ensure the output directory exists
        var outputDir = Path.GetDirectoryName(snapshotPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        _logger?.LogInformation(
            "Creating snapshot of '{SourceDirectory}' -> '{SnapshotPath}'",
            sourceDirectory, snapshotPath);

        var allFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        var fileEntries = new List<SnapshotFileEntry>(allFiles.Length);
        long totalOriginalSize = 0;

        await using var fileStream = new FileStream(
            snapshotPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536,
            useAsync: true);

        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false);

        foreach (var filePath in allFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourceDirectory, filePath)
                .Replace('\\', '/');   // normalise to forward-slash inside the ZIP

            var fileInfo = new FileInfo(filePath);
            long originalSize = fileInfo.Length;
            totalOriginalSize += originalSize;

            var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);

            await using var entryStream = entry.Open();
            await using var sourceStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 65536,
                useAsync: true);

            await sourceStream.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);

            fileEntries.Add(new SnapshotFileEntry
            {
                RelativePath = relativePath,
                OriginalSizeBytes = originalSize,
                CompressedSizeBytes = entry.CompressedLength   // available after the stream is flushed
            });

            _logger?.LogDebug(
                "Snapshot: added '{RelativePath}' ({OriginalSize} bytes)",
                relativePath, originalSize);
        }

        // Build and embed the manifest
        var manifest = new SnapshotManifest
        {
            DatabaseName = databaseName,
            TotalSizeBytes = totalOriginalSize,
            FileCount = fileEntries.Count,
            Files = fileEntries
        };

        var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        await using var manifestStream = manifestEntry.Open();
        await JsonSerializer.SerializeAsync(manifestStream, manifest, _jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        _logger?.LogInformation(
            "Snapshot created: {FileCount} files, {TotalBytes} bytes original -> '{SnapshotPath}'",
            fileEntries.Count, totalOriginalSize, snapshotPath);
    }

    /// <summary>
    /// Restore all files from a .snap archive to <paramref name="targetDirectory"/>.
    /// </summary>
    public async Task RestoreSnapshotAsync(
        string snapshotPath,
        string targetDirectory,
        bool overwrite = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        if (!File.Exists(snapshotPath))
            throw new FileNotFoundException($"Snapshot file not found: {snapshotPath}", snapshotPath);

        Directory.CreateDirectory(targetDirectory);

        _logger?.LogInformation(
            "Restoring snapshot '{SnapshotPath}' -> '{TargetDirectory}'",
            snapshotPath, targetDirectory);

        await using var fileStream = new FileStream(
            snapshotPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 65536,
            useAsync: true);

        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);

        int restoredCount = 0;

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip the metadata manifest
            if (string.Equals(entry.FullName, ManifestEntryName, StringComparison.OrdinalIgnoreCase))
                continue;

            // Build the full destination path, guarding against path-traversal attacks
            var destinationPath = Path.GetFullPath(
                Path.Combine(targetDirectory, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));

            if (!destinationPath.StartsWith(
                    Path.GetFullPath(targetDirectory) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogWarning(
                    "Skipping entry with suspicious path: '{EntryName}'", entry.FullName);
                continue;
            }

            // Create containing directory if needed
            var entryDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(entryDir))
                Directory.CreateDirectory(entryDir);

            if (!overwrite && File.Exists(destinationPath))
            {
                _logger?.LogDebug("Skipping existing file: '{DestinationPath}'", destinationPath);
                continue;
            }

            await using var entryStream = entry.Open();
            await using var destStream = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 65536,
                useAsync: true);

            await entryStream.CopyToAsync(destStream, cancellationToken).ConfigureAwait(false);

            restoredCount++;
            _logger?.LogDebug("Restored: '{EntryName}'", entry.FullName);
        }

        _logger?.LogInformation(
            "Snapshot restored: {RestoredCount} files written to '{TargetDirectory}'",
            restoredCount, targetDirectory);
    }

    /// <summary>Read the manifest from a snapshot without extracting any files.</summary>
    public async Task<SnapshotManifest> ReadManifestAsync(
        string snapshotPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath);

        if (!File.Exists(snapshotPath))
            throw new FileNotFoundException($"Snapshot file not found: {snapshotPath}", snapshotPath);

        await using var fileStream = new FileStream(
            snapshotPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 65536,
            useAsync: true);

        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);

        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException(
                $"Snapshot '{snapshotPath}' does not contain a '{ManifestEntryName}' entry.");

        await using var manifestStream = manifestEntry.Open();

        var manifest = await JsonSerializer.DeserializeAsync<SnapshotManifest>(
                manifestStream, _jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return manifest
            ?? throw new InvalidDataException(
                $"Failed to deserialize manifest from snapshot '{snapshotPath}'.");
    }

    /// <summary>List all snapshots in a directory (*.snap files).</summary>
    public IEnumerable<string> ListSnapshots(string directory)
        => Directory.GetFiles(directory, "*.snap");

    /// <summary>Export <paramref name="items"/> to a pretty-printed JSON file at <paramref name="outputPath"/>.</summary>
    public async Task ExportToJsonAsync<T>(
        IEnumerable<T> items,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        await using var fileStream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536,
            useAsync: true);

        await JsonSerializer.SerializeAsync(fileStream, items, _jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        _logger?.LogInformation("Exported JSON to '{OutputPath}'", outputPath);
    }

    /// <summary>Import items from a JSON file at <paramref name="jsonPath"/>.</summary>
    public async Task<List<T>> ImportFromJsonAsync<T>(
        string jsonPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);

        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"JSON file not found: {jsonPath}", jsonPath);

        await using var fileStream = new FileStream(
            jsonPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 65536,
            useAsync: true);

        var result = await JsonSerializer.DeserializeAsync<List<T>>(
                fileStream, _jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        _logger?.LogInformation("Imported JSON from '{JsonPath}'", jsonPath);

        return result ?? new List<T>();
    }

    /// <summary>
    /// Export <paramref name="items"/> to a CSV file using public readable properties discovered
    /// via reflection. String values that contain commas, double-quotes, or newlines are quoted
    /// and internal double-quotes are escaped per RFC 4180.
    /// </summary>
    public async Task ExportToCsvAsync<T>(
        IEnumerable<T> items,
        string csvPath,
        bool includeHeader = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);

        var outputDir = Path.GetDirectoryName(csvPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        // Discover public readable instance properties once
        var properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        await using var fileStream = new FileStream(
            csvPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536,
            useAsync: true);

        await using var writer = new StreamWriter(fileStream, Encoding.UTF8, leaveOpen: false);

        if (includeHeader)
        {
            var header = string.Join(",", properties.Select(p => EscapeCsvField(p.Name)));
            await writer.WriteLineAsync(header.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = string.Join(",", properties.Select(p =>
            {
                var value = item is null ? null : p.GetValue(item);
                return EscapeCsvField(value?.ToString());
            }));

            await writer.WriteLineAsync(row.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation("Exported CSV to '{CsvPath}'", csvPath);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Escapes a single CSV field per RFC 4180:
    /// if the value contains a comma, double-quote, or newline it is wrapped in double-quotes
    /// and any embedded double-quotes are doubled.
    /// Null/empty values are returned as an empty string.
    /// </summary>
    private static string EscapeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        bool needsQuoting = value.Contains(',')
                         || value.Contains('"')
                         || value.Contains('\n')
                         || value.Contains('\r');

        if (!needsQuoting)
            return value;

        // Escape internal double-quotes by doubling them, then wrap in double-quotes
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
