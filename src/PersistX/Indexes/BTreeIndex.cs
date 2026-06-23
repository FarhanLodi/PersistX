using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PersistX.Interfaces;

namespace PersistX.Indexes;

/// <summary>
/// Represents a single node in the B+ tree.
/// </summary>
/// <typeparam name="TKey">The type of the keys stored in this node</typeparam>
internal sealed class BTreeNode<TKey>
{
    /// <summary>Whether this node is a leaf (holds values) or an internal node (holds child pointers).</summary>
    public bool IsLeaf { get; set; }

    /// <summary>Ordered list of keys in this node.</summary>
    public List<TKey> Keys { get; } = new();

    /// <summary>
    /// Child pointers for internal nodes. Children[i] contains keys strictly less than Keys[i];
    /// Children[Keys.Count] contains keys greater than or equal to Keys[Keys.Count - 1].
    /// </summary>
    public List<BTreeNode<TKey>> Children { get; } = new();

    /// <summary>
    /// Per-key value lists for leaf nodes. Values[i] holds all values associated with Keys[i].
    /// Supports duplicate keys.
    /// </summary>
    public List<List<TValue>> GetValues<TValue>() =>
        throw new InvalidOperationException("Use the typed accessor on BTreeLeafValues.");

    /// <summary>
    /// Sibling pointer — only valid on leaf nodes; forms the leaf-chain for range scans.
    /// </summary>
    public BTreeNode<TKey>? Next { get; set; }
}

/// <summary>
/// Typed leaf-value storage attached to a <see cref="BTreeNode{TKey}"/>.
/// Kept separate so that <see cref="BTreeNode{TKey}"/> can remain non-generic.
/// </summary>
internal sealed class BTreeLeafValues<TValue>
{
    /// <summary>
    /// Parallel to <see cref="BTreeNode{TKey}.Keys"/>: Values[i] is the list of TValue items
    /// associated with Keys[i].
    /// </summary>
    public List<List<TValue>> Values { get; } = new();
}

/// <summary>
/// B+ tree index implementation for ordered data and efficient range queries.
/// </summary>
/// <remarks>
/// Order (maximum number of keys per node) = 32.
/// Leaf nodes are linked into a chain enabling O(log n + k) range scans.
/// Thread safety is provided by <see cref="ReaderWriterLockSlim"/>, allowing concurrent reads
/// with exclusive writes.
/// </remarks>
/// <typeparam name="TKey">The type of the index key (must be comparable)</typeparam>
/// <typeparam name="TValue">The type of the indexed values</typeparam>
public sealed class BTreeIndex<TKey, TValue> : IIndex<TKey, TValue>
    where TKey : notnull
{
    // ── Constants ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Maximum number of keys a node may hold before it is split.
    /// A node is considered full when Keys.Count == Order - 1.
    /// After an overflow (Keys.Count == Order) the node is split.
    /// </summary>
    private const int Order = 32;

    // ── Fields ─────────────────────────────────────────────────────────────────

    private readonly ILogger<BTreeIndex<TKey, TValue>>? _logger;
    private readonly Func<TValue, TKey> _compiledKeySelector;
    private readonly IComparer<TKey> _comparer;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

    // Root node + its companion value-store (only used when root is a leaf).
    private BTreeNode<TKey> _root;
    private BTreeLeafValues<TValue> _rootLeafValues;

    // Leftmost leaf — kept for O(1) start of full traversals.
    private BTreeNode<TKey> _leftmostLeaf;
    private BTreeLeafValues<TValue> _leftmostLeafValues;

    // Leaf value stores are mapped via object identity of their BTreeNode.
    // Using a ConcreteTable allows us to keep BTreeNode<TKey> non-generic.
    private readonly Dictionary<BTreeNode<TKey>, BTreeLeafValues<TValue>> _leafData = new(ReferenceEqualityComparer.Instance);

    private long _count;
    private bool _disposed;

    // ── IIndex properties ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public IndexType Type => IndexType.BTree;

    /// <inheritdoc/>
    public Expression<Func<TValue, TKey>> KeySelector { get; }

    // ── Constructor ────────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises a new <see cref="BTreeIndex{TKey, TValue}"/>.
    /// </summary>
    /// <param name="name">Unique name for this index.</param>
    /// <param name="keySelector">Expression that projects a <typeparamref name="TValue"/> to its key.</param>
    /// <param name="configuration">Optional index configuration (currently informational).</param>
    /// <param name="logger">Optional logger.</param>
    public BTreeIndex(
        string name,
        Expression<Func<TValue, TKey>> keySelector,
        IndexConfiguration? configuration = null,
        ILogger<BTreeIndex<TKey, TValue>>? logger = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        KeySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _compiledKeySelector = keySelector.Compile();
        _logger = logger;
        _comparer = Comparer<TKey>.Default;

        // Bootstrap an empty leaf as root.
        _root = new BTreeNode<TKey> { IsLeaf = true };
        _rootLeafValues = new BTreeLeafValues<TValue>();
        _leafData[_root] = _rootLeafValues;
        _leftmostLeaf = _root;
        _leftmostLeafValues = _rootLeafValues;
    }

    // ── IIndex methods ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task AddAsync(TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        _lock.EnterWriteLock();
        try
        {
            Insert(key, value);
            _count++;
            _logger?.LogDebug("BTreeIndex '{Name}': added key {Key}.", Name, key);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveAsync(TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        _lock.EnterWriteLock();
        try
        {
            if (DeleteValue(key, value))
            {
                _count--;
                _logger?.LogDebug("BTreeIndex '{Name}': removed value for key {Key}.", Name, key);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(TKey oldKey, TKey newKey, TValue value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        await RemoveAsync(oldKey, value, cancellationToken).ConfigureAwait(false);
        await AddAsync(newKey, value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<TValue> FindAsync(
        TKey key,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        List<TValue> snapshot;

        _lock.EnterReadLock();
        try
        {
            var (leaf, leafValues) = FindLeaf(key);
            int idx = FindKeyIndex(leaf, key);
            if (idx < 0 || idx >= leaf.Keys.Count || _comparer.Compare(leaf.Keys[idx], key) != 0)
            {
                snapshot = new List<TValue>(0);
            }
            else
            {
                snapshot = new List<TValue>(leafValues.Values[idx]);
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        foreach (var v in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return v;
            await Task.Yield();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Navigates to the leaf for <paramref name="startKey"/> in O(log n), then walks the
    /// leaf-chain forward until <paramref name="endKey"/> is exceeded — O(log n + k) total.
    /// </remarks>
    public async IAsyncEnumerable<TValue> FindRangeAsync(
        TKey startKey,
        TKey endKey,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Collect a snapshot under the read lock so we can yield outside the lock.
        List<TValue> snapshot;

        _lock.EnterReadLock();
        try
        {
            snapshot = CollectRange(startKey, endKey);
        }
        finally
        {
            _lock.ExitReadLock();
        }

        foreach (var v in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return v;
            await Task.Yield();
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<TKey> GetAllKeysAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        List<TKey> snapshot;

        _lock.EnterReadLock();
        try
        {
            snapshot = CollectAllKeys();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        foreach (var k in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return k;
            await Task.Yield();
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<TValue> GetAllValuesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        List<TValue> snapshot;

        _lock.EnterReadLock();
        try
        {
            snapshot = CollectAllValues();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        foreach (var v in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return v;
            await Task.Yield();
        }
    }

    /// <inheritdoc/>
    public Task<long> GetCountAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        _lock.EnterReadLock();
        try
        {
            return Task.FromResult(_count);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public Task<bool> ContainsKeyAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        _lock.EnterReadLock();
        try
        {
            var (leaf, _) = FindLeaf(key);
            int idx = FindKeyIndex(leaf, key);
            bool found = idx >= 0
                      && idx < leaf.Keys.Count
                      && _comparer.Compare(leaf.Keys[idx], key) == 0;
            return Task.FromResult(found);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        _lock.EnterWriteLock();
        try
        {
            _leafData.Clear();
            _root = new BTreeNode<TKey> { IsLeaf = true };
            _rootLeafValues = new BTreeLeafValues<TValue>();
            _leafData[_root] = _rootLeafValues;
            _leftmostLeaf = _root;
            _leftmostLeafValues = _rootLeafValues;
            _count = 0;
            _logger?.LogDebug("BTreeIndex '{Name}': cleared.", Name);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task RebuildAsync(IAsyncEnumerable<TValue> data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await ClearAsync(cancellationToken).ConfigureAwait(false);

        await foreach (var value in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = _compiledKeySelector(value);
            await AddAsync(key, value, cancellationToken).ConfigureAwait(false);
        }

        _logger?.LogDebug("BTreeIndex '{Name}': rebuilt with {Count} items.", Name, _count);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;

        _lock.EnterWriteLock();
        try
        {
            _leafData.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        _lock.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── Core B+ tree logic ─────────────────────────────────────────────────────

    /// <summary>
    /// Inserts <paramref name="key"/>/<paramref name="value"/> into the tree.
    /// Performs the standard top-down split-on-the-way-down approach for simplicity,
    /// implemented here as a recursive insert that bubbles promotion keys upward.
    /// </summary>
    private void Insert(TKey key, TValue value)
    {
        var result = InsertRecursive(_root, key, value);

        if (result is not null)
        {
            // The root was split; create a new root.
            var newRoot = new BTreeNode<TKey> { IsLeaf = false };
            newRoot.Keys.Add(result.PromotedKey);
            newRoot.Children.Add(_root);
            newRoot.Children.Add(result.NewNode);
            _root = newRoot;
        }
    }

    /// <summary>
    /// Recursively inserts into the subtree rooted at <paramref name="node"/>.
    /// Returns a <see cref="SplitResult"/> when this node was split, or <c>null</c> otherwise.
    /// </summary>
    private SplitResult? InsertRecursive(BTreeNode<TKey> node, TKey key, TValue value)
    {
        if (node.IsLeaf)
        {
            var leafValues = _leafData[node];
            LeafInsert(node, leafValues, key, value);

            if (node.Keys.Count < Order)
                return null;

            // Leaf overflow — split.
            return SplitLeaf(node, leafValues);
        }
        else
        {
            // Find child to descend into.
            int childIdx = FindChildIndex(node, key);
            var child = node.Children[childIdx];

            var splitResult = InsertRecursive(child, key, value);

            if (splitResult is null)
                return null;

            // Child was split; absorb the promoted key.
            int insertPos = FindChildIndex(node, splitResult.PromotedKey);
            node.Keys.Insert(insertPos, splitResult.PromotedKey);
            node.Children.Insert(insertPos + 1, splitResult.NewNode);

            if (node.Keys.Count < Order)
                return null;

            // Internal node overflow — split.
            return SplitInternal(node);
        }
    }

    /// <summary>
    /// Inserts <paramref name="key"/>/<paramref name="value"/> into a leaf in sorted order,
    /// appending to the existing value list if the key already exists.
    /// </summary>
    private void LeafInsert(BTreeNode<TKey> leaf, BTreeLeafValues<TValue> leafValues, TKey key, TValue value)
    {
        int pos = FindKeyIndex(leaf, key);

        if (pos < leaf.Keys.Count && _comparer.Compare(leaf.Keys[pos], key) == 0)
        {
            // Duplicate key — append to existing value list.
            leafValues.Values[pos].Add(value);
        }
        else
        {
            leaf.Keys.Insert(pos, key);
            leafValues.Values.Insert(pos, new List<TValue> { value });
        }
    }

    /// <summary>
    /// Splits an overfull leaf node into two leaves, threading them into the leaf chain.
    /// </summary>
    /// <returns>Split result carrying the promoted key and the new right sibling.</returns>
    private SplitResult SplitLeaf(BTreeNode<TKey> left, BTreeLeafValues<TValue> leftValues)
    {
        int mid = left.Keys.Count / 2;

        var right = new BTreeNode<TKey> { IsLeaf = true };
        var rightValues = new BTreeLeafValues<TValue>();
        _leafData[right] = rightValues;

        // Move upper half to right node.
        right.Keys.AddRange(left.Keys.GetRange(mid, left.Keys.Count - mid));
        rightValues.Values.AddRange(leftValues.Values.GetRange(mid, leftValues.Values.Count - mid));

        left.Keys.RemoveRange(mid, left.Keys.Count - mid);
        leftValues.Values.RemoveRange(mid, leftValues.Values.Count - mid);

        // Thread into leaf chain.
        right.Next = left.Next;
        left.Next = right;

        // The promoted key is the first key of the right leaf (copy-up, not push-up).
        TKey promotedKey = right.Keys[0];

        return new SplitResult(promotedKey, right);
    }

    /// <summary>
    /// Splits an overfull internal node, pushing the median key up.
    /// </summary>
    private SplitResult SplitInternal(BTreeNode<TKey> left)
    {
        int mid = left.Keys.Count / 2;
        TKey promotedKey = left.Keys[mid];

        var right = new BTreeNode<TKey> { IsLeaf = false };

        // Keys to the right of median go to right node.
        right.Keys.AddRange(left.Keys.GetRange(mid + 1, left.Keys.Count - (mid + 1)));
        left.Keys.RemoveRange(mid, left.Keys.Count - mid);   // also removes the promoted key

        // Children: right of median's children go to right node.
        right.Children.AddRange(left.Children.GetRange(mid + 1, left.Children.Count - (mid + 1)));
        left.Children.RemoveRange(mid + 1, left.Children.Count - (mid + 1));

        return new SplitResult(promotedKey, right);
    }

    /// <summary>
    /// Navigates down the tree to the leaf that would contain <paramref name="key"/>.
    /// Returns the leaf and its associated value store.
    /// </summary>
    private (BTreeNode<TKey> Leaf, BTreeLeafValues<TValue> LeafValues) FindLeaf(TKey key)
    {
        var node = _root;

        while (!node.IsLeaf)
        {
            int childIdx = FindChildIndex(node, key);
            node = node.Children[childIdx];
        }

        return (node, _leafData[node]);
    }

    /// <summary>
    /// Returns the index of the child pointer to follow for <paramref name="key"/> in an
    /// internal node.  The invariant is: children[i] covers keys &lt; keys[i].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindChildIndex(BTreeNode<TKey> node, TKey key)
    {
        int lo = 0, hi = node.Keys.Count - 1;

        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int cmp = _comparer.Compare(key, node.Keys[mid]);
            if (cmp < 0)
                hi = mid - 1;
            else
                lo = mid + 1;
        }

        return lo; // lo == number of keys that are strictly less than `key`
    }

    /// <summary>
    /// Returns the index of the first key in <paramref name="node"/> that is &gt;= <paramref name="key"/>
    /// (lower-bound binary search).  If all keys are smaller, returns <c>node.Keys.Count</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindKeyIndex(BTreeNode<TKey> node, TKey key)
    {
        int lo = 0, hi = node.Keys.Count - 1;

        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int cmp = _comparer.Compare(node.Keys[mid], key);
            if (cmp < 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return lo;
    }

    /// <summary>
    /// Removes one occurrence of <paramref name="value"/> from the leaf entry for
    /// <paramref name="key"/>.  If the value list for that key becomes empty the key is
    /// removed from the leaf (simplified delete — no rebalancing).
    /// </summary>
    /// <returns><c>true</c> when a value was actually removed.</returns>
    private bool DeleteValue(TKey key, TValue value)
    {
        var (leaf, leafValues) = FindLeaf(key);
        int idx = FindKeyIndex(leaf, key);

        if (idx >= leaf.Keys.Count || _comparer.Compare(leaf.Keys[idx], key) != 0)
            return false;

        var valueList = leafValues.Values[idx];
        bool removed = valueList.Remove(value);

        if (removed && valueList.Count == 0)
        {
            leaf.Keys.RemoveAt(idx);
            leafValues.Values.RemoveAt(idx);
        }

        return removed;
    }

    /// <summary>
    /// Collects all values whose key falls within [<paramref name="startKey"/>, <paramref name="endKey"/>]
    /// by locating the start leaf in O(log n) then following the Next chain — O(log n + k) total.
    /// </summary>
    private List<TValue> CollectRange(TKey startKey, TKey endKey)
    {
        var result = new List<TValue>();

        var (leaf, leafValues) = FindLeaf(startKey);

        // Starting position within the first leaf.
        int startIdx = FindKeyIndex(leaf, startKey);

        BTreeNode<TKey>? currentLeaf = leaf;
        BTreeLeafValues<TValue> currentLeafValues = leafValues;
        int i = startIdx;

        while (currentLeaf is not null)
        {
            while (i < currentLeaf.Keys.Count)
            {
                TKey k = currentLeaf.Keys[i];

                if (_comparer.Compare(k, endKey) > 0)
                    return result; // past end of range

                result.AddRange(currentLeafValues.Values[i]);
                i++;
            }

            // Advance to next leaf.
            var nextLeaf = currentLeaf.Next;
            if (nextLeaf is null)
                break;

            currentLeaf = nextLeaf;
            currentLeafValues = _leafData[currentLeaf];
            i = 0;
        }

        return result;
    }

    /// <summary>Traverses the leaf chain collecting every distinct key.</summary>
    private List<TKey> CollectAllKeys()
    {
        var result = new List<TKey>();
        var leaf = _leftmostLeaf;
        BTreeLeafValues<TValue>? leafValues = _leftmostLeafValues;

        while (leaf is not null)
        {
            result.AddRange(leaf.Keys);
            leaf = leaf.Next;
            if (leaf is not null)
                leafValues = _leafData[leaf];
        }

        return result;
    }

    /// <summary>Traverses the leaf chain collecting every stored value.</summary>
    private List<TValue> CollectAllValues()
    {
        var result = new List<TValue>();
        var leaf = _leftmostLeaf;

        while (leaf is not null)
        {
            var lv = _leafData[leaf];
            foreach (var valueList in lv.Values)
                result.AddRange(valueList);

            leaf = leaf.Next;
        }

        return result;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BTreeIndex<TKey, TValue>));
    }

    /// <summary>Carries the promoted key and newly created sibling out of a split operation.</summary>
    private sealed record SplitResult(TKey PromotedKey, BTreeNode<TKey> NewNode);
}
