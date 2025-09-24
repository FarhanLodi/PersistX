namespace PersistX.Enums;

/// <summary>
/// Transaction isolation levels.
/// </summary>
public enum IsolationLevel
{
    /// <summary>
    /// Read uncommitted - allows dirty reads.
    /// </summary>
    ReadUncommitted,

    /// <summary>
    /// Read committed - prevents dirty reads.
    /// </summary>
    ReadCommitted,

    /// <summary>
    /// Repeatable read - prevents dirty and non-repeatable reads.
    /// </summary>
    RepeatableRead,

    /// <summary>
    /// Serializable - highest isolation level, prevents all anomalies.
    /// </summary>
    Serializable,

    /// <summary>
    /// Snapshot isolation - uses MVCC for consistent reads.
    /// </summary>
    Snapshot
}
