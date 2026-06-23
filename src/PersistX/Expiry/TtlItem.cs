namespace PersistX.Expiry;

/// <summary>Wraps an item with an expiry timestamp.</summary>
public sealed class TtlItem<T>
{
    public T Value { get; init; } = default!;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool IsExpired(TimeProvider? timeProvider = null) =>
        (timeProvider?.GetUtcNow() ?? DateTimeOffset.UtcNow) >= ExpiresAt;

    public TimeSpan TimeRemaining(TimeProvider? timeProvider = null) =>
        ExpiresAt - (timeProvider?.GetUtcNow() ?? DateTimeOffset.UtcNow);
}
