using PersistX.Enums;

namespace PersistX.Models;

/// <summary>
/// Represents an entry in the Write-Ahead Log.
/// </summary>
public record WalEntry
{
    /// <summary>
    /// Gets or sets the entry identifier.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Gets or sets the type of WAL entry.
    /// </summary>
    public WalEntryType Type { get; init; }

    /// <summary>
    /// Gets or sets the transaction identifier.
    /// </summary>
    public long TransactionId { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when the entry was created.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the location being modified (null for commit/rollback entries).
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Gets or sets the offset within the location.
    /// </summary>
    public long Offset { get; init; }

    /// <summary>
    /// Gets or sets the data being written (empty for delete/commit/rollback entries).
    /// </summary>
    public byte[] Data { get; init; } = Array.Empty<byte>();
}
