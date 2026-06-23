using System;

namespace PersistX.Storage;

public enum WalOperationType
{
    Write,
    Delete,
    Commit,
    Rollback
}

public sealed class WalEntry
{
    public Guid EntryId { get; init; } = Guid.NewGuid();
    public Guid TransactionId { get; init; }
    public WalOperationType OperationType { get; init; }
    public string Location { get; init; } = string.Empty;
    public long Offset { get; init; }
    public byte[]? Data { get; init; }  // null for Delete/Commit/Rollback
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
