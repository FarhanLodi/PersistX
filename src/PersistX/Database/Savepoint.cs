using PersistX.Interfaces;

namespace PersistX.Database;

/// <summary>
/// Basic savepoint implementation.
/// </summary>
internal class Savepoint : ISavepoint
{
    /// <summary>
    /// Gets the savepoint name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the transaction this savepoint belongs to.
    /// </summary>
    public ITransaction Transaction { get; }

    /// <summary>
    /// Gets the timestamp when the savepoint was created.
    /// </summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the Savepoint class.
    /// </summary>
    /// <param name="name">Savepoint name</param>
    /// <param name="transaction">Transaction this savepoint belongs to</param>
    public Savepoint(string name, ITransaction transaction)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }
}
