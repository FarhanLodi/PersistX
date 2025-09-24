namespace PersistX.Models;

/// <summary>
/// Statistics about a database.
/// </summary>
public record DatabaseStatistics
{
    /// <summary>
    /// Gets or sets the number of collections.
    /// </summary>
    public int CollectionCount { get; set; }

    /// <summary>
    /// Gets or sets the total storage size in bytes.
    /// </summary>
    public long TotalStorageSize { get; set; }

    /// <summary>
    /// Gets or sets the number of active transactions.
    /// </summary>
    public int ActiveTransactionCount { get; set; }

    /// <summary>
    /// Gets or sets the last maintenance timestamp.
    /// </summary>
    public DateTime LastMaintenance { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets additional custom statistics.
    /// </summary>
    public Dictionary<string, object> CustomStats { get; set; } = new();
}
