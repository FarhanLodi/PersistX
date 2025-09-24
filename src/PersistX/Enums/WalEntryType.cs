namespace PersistX.Enums;

/// <summary>
/// Types of Write-Ahead Log entries.
/// </summary>
public enum WalEntryType
{
    /// <summary>
    /// Write operation entry.
    /// </summary>
    Write,

    /// <summary>
    /// Delete operation entry.
    /// </summary>
    Delete,

    /// <summary>
    /// Transaction commit entry.
    /// </summary>
    Commit,

    /// <summary>
    /// Transaction rollback entry.
    /// </summary>
    Rollback
}
