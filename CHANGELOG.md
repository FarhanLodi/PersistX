# Changelog

All notable changes to PersistX are documented here.

This project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [2.0.0] — 2026-06-23

This is a major release. The library has been upgraded to .NET 10, all known bugs have been fixed, and 10 new features have been added.

### Upgraded
- **Target framework**: .NET 9 → **.NET 10**
- All `Microsoft.*` and `System.*` packages updated to `10.0.0`
- Removed redundant NuGet packages now built into .NET 10 BCL: `System.Memory`, `System.IO.Compression`, `System.Threading.Channels`
- Added `MessagePack 2.5.129` for high-performance binary serialization

### Bug Fixes

- **`PersistentCollection` — index updates never worked**: The index update loop cast every index to `IIndex<object, T>`, which always fails for any typed index (e.g. `HashIndex<string, User>`). Indexes were silently never updated after `AddAsync`. Fixed with an `IIndexUpdater<TKey>` wrapper that captures the correct generic type at index creation time.
- **`PersistentCollection.GetAllAsync` — race condition**: The method read from disk without acquiring the write lock, allowing reads to observe half-written files during concurrent writes. Fixed by snapshotting the item list under the lock before yielding.
- **`PersistentCollection` — `CreatedAt` always returned `DateTime.UtcNow`**: The `CreatedAt` and `LastModified` fields in collection statistics were recalculated on every call instead of being stored and restored from metadata. Fixed by persisting these timestamps in the `.metadata` file.
- **`PersistentCollection` — broken `Serializer` property**: The `ISerializer<T> Serializer { get; }` property on `IPersistentCollection<T>` threw `NotSupportedException` in every implementation. Removed from the interface entirely.
- **`PersistentList.ClearAsync` — race condition**: `ClearAsync` wrote to disk without acquiring the `SemaphoreSlim` lock, allowing concurrent `AddAsync` or `RemoveAsync` calls to corrupt the file. Fixed by adding proper lock acquisition.
- **`FileStorage.ListLocationsAsync` — dead variable**: A `searchPath` variable was constructed but never passed to `Directory.GetFiles`. The method still worked by coincidence for simple patterns but was misleading and incorrect for subdirectory patterns. Cleaned up.
- **`Database.GetCollectionNamesAsync` — wrong storage path**: The method scanned `"collections/*"` in storage but collections are saved as `"{name}.data"`. Collections saved to disk were never rediscovered on restart. Fixed to scan `"*.data"` pattern.
- **`HashIndex` — thread-unsafe inner list**: The `ConcurrentDictionary<TKey, List<TValue>>` protected dictionary-level operations but not mutations of the inner `List<TValue>`. Concurrent adds to the same key could silently corrupt the list. Fixed by adding a dedicated `_listLock` object around all list mutations.

### New Features

#### `RemoveAsync` / `RemoveWhereAsync` / `UpdateWhereAsync`
Collections were previously append-only from the `IPersistentCollection<T>` interface. Three new methods added:
```csharp
Task<bool> RemoveAsync(T item, CancellationToken ct = default);
Task<int>  RemoveWhereAsync(Func<T, bool> predicate, CancellationToken ct = default);
Task<int>  UpdateWhereAsync(Func<T, bool> predicate, Action<T> update, CancellationToken ct = default);
Task<T?>   FirstOrDefaultAsync(Func<T, bool> predicate, CancellationToken ct = default);
IAsyncEnumerable<T> WhereAsync(Func<T, bool> predicate, CancellationToken ct = default);
```

#### `PersistentQueue<T>` — Persistent FIFO Queue
A new file-backed queue collection. Items are stored in JSON and survive app restarts. Supports `EnqueueAsync`, `DequeueAsync`, `PeekAsync`, `TryDequeueAsync`, and `EnqueueRangeAsync`.

#### `PersistentStack<T>` — Persistent LIFO Stack
A new file-backed stack collection. Supports `PushAsync`, `PopAsync`, `PeekAsync`, and `PushRangeAsync`. Ideal for undo/redo systems and history tracking.

#### `BTreeIndex<TKey, TValue>` — Ordered Range Queries
A B+ Tree index with O(log n) point lookups and O(log n + k) range queries. `FindRangeAsync(start, end)` is now efficient — previously `HashIndex.FindRangeAsync` was an O(n) full scan.

#### `PersistentHashIndex<TKey, TValue>` — Index Durability
The existing `HashIndex` was in-memory only and was wiped on every app restart. `PersistentHashIndex` serializes its data to the storage backend (`.hidx` file) and reloads it on `InitializeAsync`, eliminating the need to rebuild indexes from scratch.

#### WAL Engine — Real ACID Transactions
Three new classes implement a Write-Ahead Log for crash-safe transactions:
- `WalEntry` — a single journal record
- `WalManager` — NDJSON log file manager with append, read, and compaction
- `WalBackend` — `IBackend` decorator that journals all writes within a transaction and can commit or roll back atomically. Includes `RecoverAsync` for crash recovery on startup.

Previously, `Transaction.CommitAsync` and `RollbackAsync` were non-functional stubs that only changed an enum value.

#### LINQ Extensions — 17 Query Methods
`using PersistX.Extensions` adds LINQ-style async methods to any `IPersistentCollection<T>`:
`ToListAsync`, `ToHashSetAsync`, `OrderByAsync`, `OrderByDescendingAsync`, `TakeAsync`, `SkipAsync`, `AnyAsync`, `AllAsync`, `CountWhereAsync`, `MinAsync`, `MaxAsync`, `GroupByAsync`, `SumAsync`, `AverageAsync`, `DistinctAsync`, `PageAsync`, `BatchAsync`.

#### `MessagePackSerializer<T>` + `CompressedJsonSerializer<T>`
Two new `ISerializer<T>` implementations:
- **MessagePack**: 5–10× faster than JSON, significantly smaller payloads. Uses `ContractlessStandardResolver` so no attributes needed on your types.
- **CompressedJson**: Standard `System.Text.Json` output compressed with GZip. Best for large datasets where file size matters more than serialization speed.

#### `TtlCollection<T>` — Time-To-Live Expiry
A persistent collection where each item carries an expiry timestamp. Expired items are filtered out automatically on reads (lazy expiry) and can be explicitly purged. Accepts a `TimeProvider` for full testability.

#### `ObservableCollectionDecorator<T>` — Change Notifications
Wraps any `IPersistentCollection<T>` and publishes `CollectionChange<T>` events to a `System.Threading.Channels.Channel`. Subscribe via `WatchAsync(ct)` for a clean `IAsyncEnumerable<CollectionChange<T>>` stream, or access `Changes.Reader` directly.

#### `SnapshotManager` — Backup & Restore
Creates and restores database snapshots as ZIP archives (`.snap` files). Also provides `ExportToJsonAsync<T>`, `ImportFromJsonAsync<T>`, and `ExportToCsvAsync<T>` for collection-level data portability.

#### `ServiceCollectionExtensions` — Dependency Injection
`using PersistX.Extensions` adds DI registration helpers:
- `services.AddPersistX(options => { ... })`
- `services.AddPersistXFileDatabase(name, path)`
- `services.AddPersistXInMemoryDatabase(name)`
- `services.AddPersistXSQLiteDatabase(name, connectionString)`

### Breaking Changes

- **`IPersistentCollection<T>`**: The `ISerializer<T> Serializer { get; }` property has been removed. It threw `NotSupportedException` in every implementation and was never usable.
- **Target framework**: .NET 9 is no longer supported. Minimum runtime is now **.NET 10**.
- **`PersistX.csproj`**: If you referenced `System.Memory`, `System.IO.Compression`, or `System.Threading.Channels` as transitive dependencies via PersistX, you will need to add them directly to your own project if you use them.

### Unit Tests

Added `PersistX.UnitTests` — a dedicated xUnit test project with **130 tests** covering all collections, indexes, serializers, extensions, TTL, change notifications, WAL, and backup features.

---

## [1.0.0] — 2025-01-01

Initial release.

### Features
- `PersistentList<T>`, `PersistentDictionary<TKey, TValue>`, `PersistentSet<T>` — file-backed JSON collections
- `Database` / `PersistentCollection<T>` — database-integrated collections with index support
- Storage backends: `FileStorage`, `MemoryStorage`, `SQLiteStorage`
- `HashIndex<TKey, TValue>` — in-memory hash index
- Transaction API scaffolding (`ITransaction`, `ISavepoint`, `TransactionManager`)
- `JsonSerializer<T>` — JSON serialization via `System.Text.Json`
- Full async/await API throughout
- Targets .NET 9
