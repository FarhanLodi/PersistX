using System.Runtime.CompilerServices;
using PersistX.Interfaces;

namespace PersistX.Extensions;

/// <summary>
/// LINQ-style extension methods for <see cref="IPersistentCollection{T}"/> and <see cref="IAsyncEnumerable{T}"/>.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Materializes all elements of the collection into a <see cref="List{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="col">The persistent collection to materialize.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="List{T}"/> containing all elements.</returns>
    public static async Task<List<T>> ToListAsync<T>(
        this IPersistentCollection<T> col,
        CancellationToken ct = default)
    {
        var list = new List<T>();
        await foreach (var item in col.GetAllAsync(ct).ConfigureAwait(false))
        {
            list.Add(item);
        }
        return list;
    }

    /// <summary>
    /// Materializes all elements of the collection into a <see cref="HashSet{T}"/> using the default equality comparer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="col">The persistent collection to materialize.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="HashSet{T}"/> containing all distinct elements.</returns>
    public static async Task<HashSet<T>> ToHashSetAsync<T>(
        this IPersistentCollection<T> col,
        CancellationToken ct = default)
    {
        var set = new HashSet<T>();
        await foreach (var item in col.GetAllAsync(ct).ConfigureAwait(false))
        {
            set.Add(item);
        }
        return set;
    }

    /// <summary>
    /// Streams all elements of the collection sorted in ascending order by the specified key.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TKey">The type of the sort key; must implement <see cref="IComparable{TKey}"/>.</typeparam>
    /// <param name="col">The persistent collection to sort.</param>
    /// <param name="keySelector">A function to extract the sort key from each element.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> yielding elements in ascending key order.</returns>
    public static async IAsyncEnumerable<T> OrderByAsync<T, TKey>(
        this IPersistentCollection<T> col,
        Func<T, TKey> keySelector,
        [EnumeratorCancellation] CancellationToken ct = default)
        where TKey : IComparable<TKey>
    {
        var list = await col.ToListAsync(ct).ConfigureAwait(false);
        foreach (var item in list.OrderBy(keySelector))
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    /// <summary>
    /// Streams all elements of the collection sorted in descending order by the specified key.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TKey">The type of the sort key; must implement <see cref="IComparable{TKey}"/>.</typeparam>
    /// <param name="col">The persistent collection to sort.</param>
    /// <param name="keySelector">A function to extract the sort key from each element.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> yielding elements in descending key order.</returns>
    public static async IAsyncEnumerable<T> OrderByDescendingAsync<T, TKey>(
        this IPersistentCollection<T> col,
        Func<T, TKey> keySelector,
        [EnumeratorCancellation] CancellationToken ct = default)
        where TKey : IComparable<TKey>
    {
        var list = await col.ToListAsync(ct).ConfigureAwait(false);
        foreach (var item in list.OrderByDescending(keySelector))
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    /// <summary>
    /// Returns at most <paramref name="count"/> elements from the beginning of the async sequence.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source async enumerable.</param>
    /// <param name="count">The maximum number of elements to return.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> with at most <paramref name="count"/> elements.</returns>
    public static async IAsyncEnumerable<T> TakeAsync<T>(
        this IAsyncEnumerable<T> source,
        int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (count <= 0)
            yield break;

        var yielded = 0;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return item;
            if (++yielded >= count)
                yield break;
        }
    }

    /// <summary>
    /// Bypasses the first <paramref name="count"/> elements of the async sequence and returns the remainder.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source async enumerable.</param>
    /// <param name="count">The number of elements to skip.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that skips the first <paramref name="count"/> elements.</returns>
    public static async IAsyncEnumerable<T> SkipAsync<T>(
        this IAsyncEnumerable<T> source,
        int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var skipped = 0;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (skipped < count)
            {
                skipped++;
                continue;
            }
            yield return item;
        }
    }

    /// <summary>
    /// Determines whether any element in the collection satisfies the given predicate.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="col">The persistent collection to test.</param>
    /// <param name="predicate">A function to test each element.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><c>true</c> if at least one element matches; otherwise <c>false</c>.</returns>
    public static async Task<bool> AnyAsync<T>(
        this IPersistentCollection<T> col,
        Func<T, bool> predicate,
        CancellationToken ct = default)
    {
        var first = await col.FirstOrDefaultAsync(predicate, ct).ConfigureAwait(false);
        // For value types, FirstOrDefaultAsync returning default could be a real value,
        // so we check the WhereAsync stream directly when T is a value type.
        if (default(T) is null)
        {
            return first is not null;
        }

        await foreach (var _ in col.WhereAsync(predicate, ct).ConfigureAwait(false))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Determines whether all elements in the collection satisfy the given predicate.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="col">The persistent collection to test.</param>
    /// <param name="predicate">A function to test each element.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><c>true</c> if every element matches (or the collection is empty); otherwise <c>false</c>.</returns>
    public static async Task<bool> AllAsync<T>(
        this IPersistentCollection<T> col,
        Func<T, bool> predicate,
        CancellationToken ct = default)
    {
        await foreach (var item in col.GetAllAsync(ct).ConfigureAwait(false))
        {
            if (!predicate(item))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Counts the number of elements in the collection that satisfy the given predicate.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="col">The persistent collection to count.</param>
    /// <param name="predicate">A function to test each element.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The number of elements that match the predicate.</returns>
    public static async Task<long> CountWhereAsync<T>(
        this IPersistentCollection<T> col,
        Func<T, bool> predicate,
        CancellationToken ct = default)
    {
        long count = 0;
        await foreach (var _ in col.WhereAsync(predicate, ct).ConfigureAwait(false))
        {
            count++;
        }
        return count;
    }

    /// <summary>
    /// Returns the minimum value produced by the selector over all elements, or <c>default</c> if the collection is empty.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TKey">The type of the comparable value; must implement <see cref="IComparable{TKey}"/>.</typeparam>
    /// <param name="col">The persistent collection to scan.</param>
    /// <param name="selector">A function to extract a comparable value from each element.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The minimum projected value, or <c>default</c> if the collection is empty.</returns>
    public static async Task<TKey?> MinAsync<T, TKey>(
        this IPersistentCollection<T> col,
        Func<T, TKey> selector,
        CancellationToken ct = default)
        where TKey : IComparable<TKey>
    {
        TKey? min = default;
        var hasValue = false;

        await foreach (var item in col.GetAllAsync(ct).ConfigureAwait(false))
        {
            var value = selector(item);
            if (!hasValue || value.CompareTo(min!) < 0)
            {
                min = value;
                hasValue = true;
            }
        }

        return min;
    }

    /// <summary>
    /// Returns the maximum value produced by the selector over all elements, or <c>default</c> if the collection is empty.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TKey">The type of the comparable value; must implement <see cref="IComparable{TKey}"/>.</typeparam>
    /// <param name="col">The persistent collection to scan.</param>
    /// <param name="selector">A function to extract a comparable value from each element.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The maximum projected value, or <c>default</c> if the collection is empty.</returns>
    public static async Task<TKey?> MaxAsync<T, TKey>(
        this IPersistentCollection<T> col,
        Func<T, TKey> selector,
        CancellationToken ct = default)
        where TKey : IComparable<TKey>
    {
        TKey? max = default;
        var hasValue = false;

        await foreach (var item in col.GetAllAsync(ct).ConfigureAwait(false))
        {
            var value = selector(item);
            if (!hasValue || value.CompareTo(max!) > 0)
            {
                max = value;
                hasValue = true;
            }
        }

        return max;
    }

    /// <summary>
    /// Groups all elements by a key and returns a dictionary mapping each key to its list of elements.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TKey">The type of the grouping key; must be non-null.</typeparam>
    /// <param name="col">The persistent collection to group.</param>
    /// <param name="keySelector">A function to extract the grouping key from each element.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Dictionary{TKey,TValue}"/> where each value is the list of elements sharing that key.</returns>
    public static async Task<Dictionary<TKey, List<T>>> GroupByAsync<T, TKey>(
        this IPersistentCollection<T> col,
        Func<T, TKey> keySelector,
        CancellationToken ct = default)
        where TKey : notnull
    {
        var groups = new Dictionary<TKey, List<T>>();

        await foreach (var item in col.GetAllAsync(ct).ConfigureAwait(false))
        {
            var key = keySelector(item);
            if (!groups.TryGetValue(key, out var bucket))
            {
                bucket = new List<T>();
                groups[key] = bucket;
            }
            bucket.Add(item);
        }

        return groups;
    }

    /// <summary>
    /// Computes the sum of values produced by the selector over all elements.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="col">The persistent collection to sum.</param>
    /// <param name="selector">A function to extract a <see cref="double"/> value from each element.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The sum of all projected values, or <c>0</c> if the collection is empty.</returns>
    public static async Task<double> SumAsync<T>(
        this IPersistentCollection<T> col,
        Func<T, double> selector,
        CancellationToken ct = default)
    {
        var sum = 0d;
        await foreach (var item in col.GetAllAsync(ct).ConfigureAwait(false))
        {
            sum += selector(item);
        }
        return sum;
    }

    /// <summary>
    /// Computes the average of values produced by the selector over all elements.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="col">The persistent collection to average.</param>
    /// <param name="selector">A function to extract a <see cref="double"/> value from each element.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The arithmetic mean of all projected values.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the collection is empty.</exception>
    public static async Task<double> AverageAsync<T>(
        this IPersistentCollection<T> col,
        Func<T, double> selector,
        CancellationToken ct = default)
    {
        var sum = 0d;
        var count = 0L;

        await foreach (var item in col.GetAllAsync(ct).ConfigureAwait(false))
        {
            sum += selector(item);
            count++;
        }

        if (count == 0)
            throw new InvalidOperationException("Cannot compute average of an empty collection.");

        return sum / count;
    }

    /// <summary>
    /// Streams distinct elements from the collection using the default equality comparer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="col">The persistent collection to deduplicate.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> yielding each unique element exactly once.</returns>
    public static async IAsyncEnumerable<T> DistinctAsync<T>(
        this IPersistentCollection<T> col,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var seen = new HashSet<T>();
        await foreach (var item in col.GetAllAsync(ct).ConfigureAwait(false))
        {
            if (seen.Add(item))
                yield return item;
        }
    }

    /// <summary>
    /// Streams a single page of elements from the collection.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="col">The persistent collection to page.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="pageSize">The number of elements per page.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> yielding the elements on the requested page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pageIndex"/> is negative or <paramref name="pageSize"/> is less than 1.
    /// </exception>
    public static async IAsyncEnumerable<T> PageAsync<T>(
        this IPersistentCollection<T> col,
        int pageIndex,
        int pageSize,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (pageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page index must be zero or greater.");
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be at least 1.");

        var skip = pageIndex * pageSize;
        var taken = 0;

        await foreach (var item in col.GetAllAsync(ct).ConfigureAwait(false))
        {
            if (skip > 0)
            {
                skip--;
                continue;
            }

            yield return item;

            if (++taken >= pageSize)
                yield break;
        }
    }

    /// <summary>
    /// Partitions all elements of the collection into consecutive batches of at most <paramref name="batchSize"/> elements.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="col">The persistent collection to batch.</param>
    /// <param name="batchSize">The maximum number of elements in each batch.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="List{T}"/> where each list contains up to
    /// <paramref name="batchSize"/> elements. The final batch may contain fewer elements.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> is less than 1.</exception>
    public static async IAsyncEnumerable<List<T>> BatchAsync<T>(
        this IPersistentCollection<T> col,
        int batchSize,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be at least 1.");

        var batch = new List<T>(batchSize);

        await foreach (var item in col.GetAllAsync(ct).ConfigureAwait(false))
        {
            batch.Add(item);
            if (batch.Count == batchSize)
            {
                yield return batch;
                batch = new List<T>(batchSize);
            }
        }

        if (batch.Count > 0)
            yield return batch;
    }
}
