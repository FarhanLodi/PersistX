namespace PersistX.Models;

/// <summary>
/// Index information for Write-Ahead Log management.
/// </summary>
public record WalIndex
{
    /// <summary>
    /// Gets or sets the next log entry identifier to be allocated.
    /// </summary>
    public long NextLogEntryId { get; init; }

    /// <summary>
    /// Gets or sets the last committed entry identifier.
    /// </summary>
    public long LastCommittedEntryId { get; init; }
}
