namespace PersistX.Backup;

/// <summary>Metadata about a snapshot archive.</summary>
public sealed class SnapshotManifest
{
    public string Version { get; init; } = "2.0";
    public required string DatabaseName { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public long TotalSizeBytes { get; init; }
    public int FileCount { get; init; }
    public List<SnapshotFileEntry> Files { get; init; } = new();
}

public sealed class SnapshotFileEntry
{
    public required string RelativePath { get; init; }
    public long OriginalSizeBytes { get; init; }
    public long CompressedSizeBytes { get; init; }
}
