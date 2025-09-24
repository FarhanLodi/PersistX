namespace PersistX.Enums;

/// <summary>
/// Transaction states.
/// </summary>
public enum TransactionState
{
    /// <summary>
    /// Transaction is active and can accept operations.
    /// </summary>
    Active,

    /// <summary>
    /// Transaction is being committed.
    /// </summary>
    Committing,

    /// <summary>
    /// Transaction has been committed successfully.
    /// </summary>
    Committed,

    /// <summary>
    /// Transaction is being rolled back.
    /// </summary>
    RollingBack,

    /// <summary>
    /// Transaction has been rolled back.
    /// </summary>
    RolledBack,

    /// <summary>
    /// Transaction has been aborted due to an error.
    /// </summary>
    Aborted
}
