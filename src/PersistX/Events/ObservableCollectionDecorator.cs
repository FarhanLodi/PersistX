using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using PersistX.Interfaces;

namespace PersistX.Events;

/// <summary>
/// Wraps a persistent collection and publishes change notifications via a Channel.
/// Subscribe via <see cref="WatchAsync"/> or read directly from <see cref="Changes"/>.Reader.
/// </summary>
public sealed class ObservableCollectionDecorator<T> : IPersistentCollection<T>
{
    private readonly IPersistentCollection<T> _inner;
    private readonly Channel<CollectionChange<T>> _channel;
    private bool _disposed;

    /// <param name="inner">The underlying persistent collection to wrap.</param>
    /// <param name="channelCapacity">
    /// Bounded capacity of the change channel. When the channel is full the oldest
    /// unread change is silently dropped so that writers are never blocked.
    /// </param>
    public ObservableCollectionDecorator(IPersistentCollection<T> inner, int channelCapacity = 1000)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _channel = Channel.CreateBounded<CollectionChange<T>>(new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = false
        });
    }

    // -------------------------------------------------------------------------
    // Observable surface
    // -------------------------------------------------------------------------

    /// <summary>The underlying changes channel. Read from <c>Changes.Reader</c> to receive events.</summary>
    public Channel<CollectionChange<T>> Changes => _channel;

    /// <summary>
    /// Returns an async stream of every change event. The stream completes when
    /// the collection is disposed or <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    public async IAsyncEnumerable<CollectionChange<T>> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var change in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return change;
    }

    // -------------------------------------------------------------------------
    // IPersistentCollection<T> — mutating operations (publish notifications)
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task AddAsync(T item, CancellationToken ct = default)
    {
        await _inner.AddAsync(item, ct).ConfigureAwait(false);
        PublishChange(new CollectionChange<T>
        {
            ChangeType = CollectionChangeType.Added,
            Item = item,
            CollectionName = Name
        });
    }

    /// <inheritdoc/>
    public async Task AddRangeAsync(IEnumerable<T> items, CancellationToken ct = default)
    {
        // Materialise once so we can both pass to inner and attach to the event.
        var list = items as IReadOnlyList<T> ?? items.ToList();
        await _inner.AddRangeAsync(list, ct).ConfigureAwait(false);
        PublishChange(new CollectionChange<T>
        {
            ChangeType = CollectionChangeType.RangeAdded,
            Items = list,
            CollectionName = Name,
            AffectedCount = list.Count
        });
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveAsync(T item, CancellationToken ct = default)
    {
        bool removed = await _inner.RemoveAsync(item, ct).ConfigureAwait(false);
        if (removed)
        {
            PublishChange(new CollectionChange<T>
            {
                ChangeType = CollectionChangeType.Removed,
                Item = item,
                CollectionName = Name
            });
        }
        return removed;
    }

    /// <inheritdoc/>
    public async Task<int> RemoveWhereAsync(Func<T, bool> predicate, CancellationToken ct = default)
    {
        int count = await _inner.RemoveWhereAsync(predicate, ct).ConfigureAwait(false);
        if (count > 0)
        {
            PublishChange(new CollectionChange<T>
            {
                ChangeType = CollectionChangeType.Removed,
                CollectionName = Name,
                AffectedCount = count
            });
        }
        return count;
    }

    /// <inheritdoc/>
    public async Task<int> UpdateWhereAsync(Func<T, bool> predicate, Action<T> update, CancellationToken ct = default)
    {
        int count = await _inner.UpdateWhereAsync(predicate, update, ct).ConfigureAwait(false);
        if (count > 0)
        {
            PublishChange(new CollectionChange<T>
            {
                ChangeType = CollectionChangeType.Updated,
                CollectionName = Name,
                AffectedCount = count
            });
        }
        return count;
    }

    /// <inheritdoc/>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        await _inner.ClearAsync(ct).ConfigureAwait(false);
        PublishChange(new CollectionChange<T>
        {
            ChangeType = CollectionChangeType.Cleared,
            CollectionName = Name,
            AffectedCount = 0
        });
    }

    // -------------------------------------------------------------------------
    // IPersistentCollection<T> — read-only / structural operations (delegate only)
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public string Name => _inner.Name;

    /// <inheritdoc/>
    public Task<long> CountAsync => _inner.CountAsync;

    /// <inheritdoc/>
    public bool IsReadOnly => _inner.IsReadOnly;

    /// <inheritdoc/>
    public Task<bool> ContainsAsync(T item, CancellationToken ct = default)
        => _inner.ContainsAsync(item, ct);

    /// <inheritdoc/>
    public Task<T?> FirstOrDefaultAsync(Func<T, bool> predicate, CancellationToken ct = default)
        => _inner.FirstOrDefaultAsync(predicate, ct);

    /// <inheritdoc/>
    public IAsyncEnumerable<T> WhereAsync(Func<T, bool> predicate, CancellationToken ct = default)
        => _inner.WhereAsync(predicate, ct);

    /// <inheritdoc/>
    public IAsyncEnumerable<T> GetAllAsync(CancellationToken ct = default)
        => _inner.GetAllAsync(ct);

    /// <inheritdoc/>
    public Task<IIndex<TKey, T>> CreateIndexAsync<TKey>(
        string name,
        Expression<Func<T, TKey>> keySelector,
        IndexConfiguration? config = null,
        CancellationToken ct = default) where TKey : notnull
        => _inner.CreateIndexAsync(name, keySelector, config, ct);

    /// <inheritdoc/>
    public Task<IIndex<TKey, T>?> GetIndexAsync<TKey>(string name, CancellationToken ct = default)
        where TKey : notnull
        => _inner.GetIndexAsync<TKey>(name, ct);

    /// <inheritdoc/>
    public Task DropIndexAsync(string name, CancellationToken ct = default)
        => _inner.DropIndexAsync(name, ct);

    /// <inheritdoc/>
    public IAsyncEnumerable<string> GetIndexNamesAsync(CancellationToken ct = default)
        => _inner.GetIndexNamesAsync(ct);

    /// <inheritdoc/>
    public Task RebuildIndexesAsync(CancellationToken ct = default)
        => _inner.RebuildIndexesAsync(ct);

    /// <inheritdoc/>
    public Task FlushAsync(CancellationToken ct = default)
        => _inner.FlushAsync(ct);

    /// <inheritdoc/>
    public Task<CollectionStatistics> GetStatisticsAsync(CancellationToken ct = default)
        => _inner.GetStatisticsAsync(ct);

    /// <inheritdoc/>
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
        => _inner.GetAsyncEnumerator(ct);

    // -------------------------------------------------------------------------
    // IAsyncDisposable
    // -------------------------------------------------------------------------

    /// <summary>
    /// Completes the change channel (signalling all <see cref="WatchAsync"/> consumers)
    /// and then disposes the inner collection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Complete the channel so WatchAsync streams terminate cleanly.
        _channel.Writer.TryComplete();

        await _inner.DisposeAsync().ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Non-blocking write to the channel. If the channel is full the oldest
    /// event is silently dropped (configured via <see cref="BoundedChannelFullMode.DropOldest"/>).
    /// </summary>
    private void PublishChange(CollectionChange<T> change)
        => _channel.Writer.TryWrite(change);
}
