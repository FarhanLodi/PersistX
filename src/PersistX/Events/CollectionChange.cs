namespace PersistX.Events;

/// <summary>Identifies what kind of mutation occurred on a persistent collection.</summary>
public enum CollectionChangeType
{
    Added,
    Removed,
    Updated,
    Cleared,
    RangeAdded
}

/// <summary>Represents a change event on a persistent collection.</summary>
public sealed record CollectionChange<T>
{
    /// <summary>The kind of change that occurred.</summary>
    public required CollectionChangeType ChangeType { get; init; }

    /// <summary>The item involved in an Added, Removed, or Updated operation.</summary>
    public T? Item { get; init; }

    /// <summary>The previous value of the item before an Updated operation.</summary>
    public T? OldItem { get; init; }

    /// <summary>The full set of items added during a RangeAdded operation.</summary>
    public IReadOnlyList<T>? Items { get; init; }

    /// <summary>The name of the collection that produced this change.</summary>
    public required string CollectionName { get; init; }

    /// <summary>UTC timestamp at which the change was recorded.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Number of items affected. Always 1 for single-item operations;
    /// set to the actual count for RangeAdded and bulk-remove operations.
    /// </summary>
    public int AffectedCount { get; init; } = 1;
}
