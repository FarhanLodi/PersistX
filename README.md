<div align="center">
  <img src="assets/PersistX.png" alt="PersistX Logo" width="180"/>

  <h1>PersistX</h1>

  <p><strong>Persistent collections for .NET — built like a database, used like a list.</strong></p>

  <p>
    <a href="https://www.nuget.org/packages/PersistX"><img src="https://img.shields.io/nuget/v/PersistX.svg?style=flat-square&label=nuget" alt="NuGet"/></a>
    <a href="https://www.nuget.org/packages/PersistX"><img src="https://img.shields.io/nuget/dt/PersistX.svg?style=flat-square&color=blue" alt="Downloads"/></a>
    <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="MIT License"/></a>
    <img src="https://img.shields.io/badge/.NET-10.0-purple?style=flat-square" alt=".NET 10"/>
    <img src="https://img.shields.io/badge/tests-130%20passing-brightgreen?style=flat-square" alt="Tests"/>
  </p>

  <br/>

  <p>
    <a href="#-quick-start">Quick Start</a> •
    <a href="#-file-based-collections">File-Based</a> •
    <a href="#-database-collections">Database</a> •
    <a href="#-advanced-features">Advanced Features</a> •
    <a href="#-api-reference">API Reference</a> •
    <a href="CHANGELOG.md">Changelog</a>
  </p>
</div>

---

## What is PersistX?

PersistX is an embedded persistence library for .NET that lets you store and query data using familiar collection patterns — no SQL, no database server, no configuration required.

Your data is automatically saved to disk and reloaded when your app starts. Use it for anything that needs to survive a restart: application state, user data, job queues, caches, settings, or small-to-medium datasets.

```csharp
// Looks like a regular list — but data is saved to disk automatically
var users = new PersistentList<User>("users.json");

await users.AddAsync(new User { Name = "Alice", Email = "alice@example.com" });
await users.AddAsync(new User { Name = "Bob",   Email = "bob@example.com"   });

// Restart your app — data is still here
var count = await users.CountAsync(); // 2
```

---

## Why PersistX?

| | PersistX | Plain files | SQLite direct | Full database |
|---|:---:|:---:|:---:|:---:|
| No setup or config | ✅ | ✅ | ❌ | ❌ |
| Familiar .NET API | ✅ | ❌ | ❌ | ❌ |
| ACID transactions | ✅ | ❌ | ✅ | ✅ |
| Indexes & fast queries | ✅ | ❌ | ✅ | ✅ |
| Change notifications | ✅ | ❌ | ❌ | varies |
| Built-in backup/export | ✅ | ❌ | ❌ | varies |
| TTL / auto-expiry | ✅ | ❌ | ❌ | varies |
| Works without SQL | ✅ | ✅ | ❌ | ❌ |

---

## Installation

```bash
dotnet add package PersistX
```

```xml
<!-- Or in your .csproj -->
<PackageReference Include="PersistX" Version="2.0.0" />
```

**Requirements:** .NET 10.0 or later.

---

## Quick Start

### Simplest possible usage

```csharp
using PersistX.FileBased;

// A list that saves itself to disk
var notes = new PersistentList<string>("notes.json");
await notes.AddAsync("Buy milk");
await notes.AddAsync("Call dentist");

// A dictionary for key-value settings
var config = new PersistentDictionary<string, string>("config.json");
await config.SetAsync("theme", "dark");
await config.SetAsync("lang", "en");

// A queue for background job processing
var jobs = new PersistentQueue<string>("jobs.json");
await jobs.EnqueueAsync("send-welcome-email");
var next = await jobs.DequeueAsync(); // "send-welcome-email"
```

### With indexes and transactions

```csharp
using PersistX.Database;

var db = await DatabaseFactory.CreateFileDatabaseAsync("MyApp", "./data");

var users = await db.CreateCollectionAsync<User>("users");
await users.CreateIndexAsync("by_email", u => u.Email);

// Add with transaction — either both save or neither does
await db.ExecuteInTransactionAsync(async tx =>
{
    await users.AddAsync(new User { Name = "Alice", Email = "alice@example.com" });
    await orders.AddAsync(new Order { UserId = "alice", Total = 49.99m });
});

// Find by indexed field — O(1) lookup
var idx = await users.GetIndexAsync<string>("by_email");
await foreach (var user in idx.FindAsync("alice@example.com"))
    Console.WriteLine(user.Name);
```

---

## File-Based Collections

The simplest mode. Each collection is a single JSON file. No database, no factory, no initialization — just a path.

All collections are **thread-safe** and have **in-memory caching** to avoid repeated disk reads.

---

### `PersistentList<T>`

An ordered list backed by a JSON file.

```csharp
var list = new PersistentList<Product>("products.json");

// Add
await list.AddAsync(new Product { Name = "Widget", Price = 9.99m });
await list.AddRangeAsync(productBatch);

// Read
var all    = await list.ToListAsync();
var item   = await list.GetAtAsync(0);
var count  = await list.CountAsync();
var exists = await list.ContainsAsync(product);
var index  = await list.IndexOfAsync(product);

// Update
await list.SetAtAsync(0, updatedProduct);
await list.InsertAsync(0, firstProduct);

// Remove
await list.RemoveAsync(product);
await list.RemoveAtAsync(0);
await list.ClearAsync();

// Sort
await list.SortAsync();
await list.SortAsync(Comparer<Product>.Create((a, b) => a.Price.CompareTo(b.Price)));

// Enumerate
await foreach (var p in list.GetAllAsync())
    Console.WriteLine(p.Name);

// Slice
var page = await list.GetRangeAsync(startIndex: 0, count: 10);
```

---

### `PersistentDictionary<TKey, TValue>`

A key-value store backed by a JSON file.

```csharp
var dict = new PersistentDictionary<string, string>("settings.json");

await dict.SetAsync("theme", "dark");
await dict.SetAsync("language", "en-US");

var theme   = await dict.GetAsync("theme");             // "dark"
var has     = await dict.ContainsKeyAsync("theme");     // true
var keys    = await dict.GetAllKeysAsync();
var values  = await dict.GetAllValuesAsync();
var count   = await dict.CountAsync();

await dict.RemoveAsync("theme");
await dict.ClearAsync();
```

---

### `PersistentSet<T>`

An unordered collection of unique values.

```csharp
var tags = new PersistentSet<string>("tags.json");

await tags.AddAsync("dotnet");
await tags.AddAsync("dotnet"); // silently ignored — already present
await tags.AddAsync("csharp");

var count = await tags.CountAsync();           // 2
var has   = await tags.ContainsAsync("dotnet"); // true
await tags.RemoveAsync("dotnet");
```

---

### `PersistentQueue<T>`

A FIFO queue — items come out in the order they went in. Survives restarts.

```csharp
var queue = new PersistentQueue<Job>("jobs.json");

// Producer
await queue.EnqueueAsync(new Job { Id = 1, Task = "Send invoice" });
await queue.EnqueueAsync(new Job { Id = 2, Task = "Generate report" });
await queue.EnqueueRangeAsync(morJobs);

// Consumer
var next   = await queue.DequeueAsync();           // Job 1 — removed from queue
var peek   = await queue.PeekAsync();              // Job 2 — NOT removed
var (ok, job) = await queue.TryDequeueAsync();     // safe — no exception if empty
var empty  = await queue.IsEmptyAsync();
var count  = await queue.CountAsync();

await queue.ClearAsync();
```

**Common use cases:** Background job queues, task processing pipelines, event outboxes.

---

### `PersistentStack<T>`

A LIFO stack — the last item pushed is the first item popped. Survives restarts.

```csharp
var stack = new PersistentStack<Command>("undo.json");

// Push commands as user performs actions
await stack.PushAsync(new Command { Action = "CreateFile", Path = "report.txt" });
await stack.PushAsync(new Command { Action = "WriteText",  Path = "report.txt" });

// Pop to undo
var lastAction = await stack.PopAsync();  // WriteText — removed
var preview    = await stack.PeekAsync(); // CreateFile — NOT removed
var count      = await stack.CountAsync();

await stack.ClearAsync();
```

**Common use cases:** Undo/redo history, navigation stacks, call history.

---

## Database Collections

For more demanding scenarios: multiple indexes, ACID transactions, complex queries, and large datasets.

### Setting Up a Database

```csharp
using PersistX.Database;

// File-backed (recommended for production)
var db = await DatabaseFactory.CreateFileDatabaseAsync("MyApp", "./data/myapp.db");

// SQLite-backed (best for complex data + SQL compatibility)
var db = await DatabaseFactory.CreateSQLiteDatabaseAsync("MyApp", "Data Source=myapp.db");

// In-memory (ideal for testing)
var db = await DatabaseFactory.CreateInMemoryDatabaseAsync("TestDb");
```

### Creating Collections

```csharp
// Create a typed collection
var users = await db.CreateCollectionAsync<User>("users");

// Get an existing collection
var users = await db.GetCollectionAsync<User>("users");

// List all collections in the database
await foreach (var name in db.GetCollectionNamesAsync())
    Console.WriteLine(name);

// Delete a collection
await db.DropCollectionAsync("users");
```

### Indexes

Indexes dramatically speed up queries on large collections.

```csharp
// Hash Index — O(1) exact-match lookups
await users.CreateIndexAsync("by_email", u => u.Email);

// B+ Tree Index — O(log n) exact match AND range queries
await users.CreateIndexAsync("by_age", u => u.Age,
    new IndexConfiguration { Type = IndexType.BTree });

// Unique index — rejects duplicate keys
await users.CreateIndexAsync("by_username", u => u.Username,
    new IndexConfiguration { IsUnique = true });

// Use an index
var emailIdx = await users.GetIndexAsync<string>("by_email");
await foreach (var user in emailIdx.FindAsync("alice@example.com"))
    Console.WriteLine(user.Name);

// Range query (requires BTree index)
var ageIdx = await users.GetIndexAsync<int>("by_age");
await foreach (var user in ageIdx.FindRangeAsync(18, 65))
    Console.WriteLine($"{user.Name}, {user.Age}");

// Manage indexes
var names = users.GetIndexNamesAsync();
await users.DropIndexAsync("by_email");
await users.RebuildIndexesAsync();
```

### Full CRUD

```csharp
// Create
await users.AddAsync(new User { Name = "Alice", Email = "alice@example.com", Age = 30 });
await users.AddRangeAsync(userList);

// Read
var count  = await users.CountAsync;
var first  = await users.FirstOrDefaultAsync(u => u.Name == "Alice");
var exists = await users.ContainsAsync(user);

await foreach (var user in users.GetAllAsync())
    Console.WriteLine(user.Name);

await foreach (var admin in users.WhereAsync(u => u.IsAdmin))
    Console.WriteLine(admin.Name);

// Update
await users.UpdateWhereAsync(
    predicate: u => u.Name == "Alice",
    update:    u => { u.Age = 31; u.LastSeen = DateTime.UtcNow; });

// Delete
await users.RemoveAsync(alice);
await users.RemoveWhereAsync(u => u.IsDeleted);
await users.ClearAsync();

// Stats
var stats = await users.GetStatisticsAsync();
Console.WriteLine($"{stats.ElementCount} users, {stats.StorageSize} bytes, {stats.IndexCount} indexes");
```

### Transactions

```csharp
// Option 1 — automatic (recommended for most cases)
await db.ExecuteInTransactionAsync(async tx =>
{
    await users.AddAsync(newUser);
    await wallets.AddAsync(newWallet);
    // If either throws, both are rolled back automatically
});

// Option 2 — manual control
await using var tx = await db.BeginTransactionAsync();
try
{
    await users.AddAsync(newUser);
    await orders.AddAsync(newOrder);
    await tx.CommitAsync();
}
catch
{
    await tx.RollbackAsync();
    throw;
}

// Option 3 — with savepoints (partial rollback)
await using var tx = await db.BeginTransactionAsync();
var sp = await tx.CreateSavepointAsync("before_orders");
try
{
    await orders.AddAsync(newOrder);
}
catch
{
    await tx.RollbackToSavepointAsync(sp); // only rolls back orders
}
await tx.CommitAsync();

// Option 4 — read-only transaction (no locking overhead)
var result = await db.ExecuteInTransactionAsync(async tx =>
{
    return await users.CountAsync;
}, IsolationLevel.ReadOnly);
```

---

## LINQ Extensions

Add `using PersistX.Extensions;` to unlock LINQ-style query methods on any persistent collection.

```csharp
using PersistX.Extensions;

// Materialize
var list = await users.ToListAsync();
var set  = await users.ToHashSetAsync();

// Filter & sort
var sorted    = users.OrderByAsync(u => u.Name);
var descOrder = users.OrderByDescendingAsync(u => u.CreatedAt);
var filtered  = users.WhereAsync(u => u.IsActive);
var distinct  = users.DistinctAsync();

// Pagination
var page = users.PageAsync(pageIndex: 2, pageSize: 20);
await foreach (var batch in users.BatchAsync(batchSize: 100))
    await ProcessBatchAsync(batch);

// Projections (chaining with TakeAsync / SkipAsync)
var top10 = users.OrderByAsync(u => u.Score).TakeAsync(10);
var skip5 = users.GetAllAsync().SkipAsync(5);

// Aggregates
bool anyAdmins = await users.AnyAsync(u => u.IsAdmin);
bool allActive = await users.AllAsync(u => u.IsActive);
long adultCount = await users.CountWhereAsync(u => u.Age >= 18);

int    oldest  = await users.MaxAsync(u => u.Age);
int    youngest = await users.MinAsync(u => u.Age);
double avgAge  = await users.AverageAsync(u => (double)u.Age);
double total   = await users.SumAsync(u => (double)u.Balance);

// Grouping
var byCountry = await users.GroupByAsync(u => u.Country);
foreach (var (country, members) in byCountry)
    Console.WriteLine($"{country}: {members.Count} users");
```

---

## Advanced Features

### TTL Collections — Auto-Expiring Items

Items are automatically filtered out once their TTL expires.

```csharp
using PersistX.Expiry;

var sessions = new TtlCollection<Session>("sessions.json");

// Add with a TTL
await sessions.AddAsync(userSession, ttl: TimeSpan.FromMinutes(30));
await sessions.AddAsync(adminSession, ttl: TimeSpan.FromHours(8));

// Reads automatically hide expired items
var active  = await sessions.GetAllValidAsync();
var single  = await sessions.GetAsync(s => s.UserId == "alice");
var count   = await sessions.CountAsync();       // only non-expired
var total   = await sessions.TotalCountAsync();  // including expired

// Explicitly remove expired items from disk
int purged = await sessions.PurgeExpiredAsync();

// Remove a specific item
await sessions.RemoveAsync(s => s.UserId == "alice");
```

> **Testable time**: `TtlCollection` accepts a `TimeProvider` parameter, so you can fast-forward time in unit tests without `Thread.Sleep`.

---

### Change Notifications — React to Data Changes

Wrap any collection with `ObservableCollectionDecorator<T>` to get a real-time stream of changes.

```csharp
using PersistX.Events;

var observable = new ObservableCollectionDecorator<Order>(ordersCollection);

// Option 1 — async stream (great for background listeners)
_ = Task.Run(async () =>
{
    await foreach (var change in observable.WatchAsync(cts.Token))
    {
        Console.WriteLine($"[{change.ChangeType}] {change.Item?.OrderId} at {change.Timestamp}");
    }
});

// Option 2 — Channel reader (non-blocking, good for polling)
if (observable.Changes.Reader.TryRead(out var change))
    ProcessChange(change);

// All writes go through the decorator exactly as before
await observable.AddAsync(newOrder);           // → publishes Added
await observable.RemoveAsync(cancelledOrder);  // → publishes Removed
await observable.ClearAsync();                 // → publishes Cleared

// Change event shape
public record CollectionChange<T>
{
    CollectionChangeType ChangeType { get; }  // Added, Removed, Updated, Cleared, RangeAdded
    T? Item { get; }
    T? OldItem { get; }                       // previous value for Updated events
    string CollectionName { get; }
    DateTimeOffset Timestamp { get; }
    int AffectedCount { get; }                // for bulk operations
}
```

**Common use cases:** Real-time UI updates, audit logging, cache invalidation, event-driven sync.

---

### Backup & Restore — Snapshots

Create point-in-time snapshots of your database as a single portable `.snap` file (ZIP archive).

```csharp
using PersistX.Backup;

var manager = new SnapshotManager();

// Create a snapshot of your entire database directory
await manager.CreateSnapshotAsync(
    sourceDirectory: "./data",
    snapshotPath:    $"./backups/backup_{DateTime.Today:yyyyMMdd}.snap",
    databaseName:   "MyApp");

// Inspect a snapshot without restoring
var manifest = await manager.ReadManifestAsync("./backups/backup_20260623.snap");
Console.WriteLine($"Snapshot: {manifest.DatabaseName}, {manifest.FileCount} files, {manifest.TotalSizeBytes} bytes");

// Restore to a directory
await manager.RestoreSnapshotAsync(
    snapshotPath:    "./backups/backup_20260623.snap",
    targetDirectory: "./data_restored");

// List all available snapshots
foreach (var snap in manager.ListSnapshots("./backups"))
    Console.WriteLine(snap);

// Export a collection to CSV or JSON (no ZIP needed)
await manager.ExportToCsvAsync(users, "users_export.csv");
await manager.ExportToJsonAsync(users, "users_export.json");
var imported = await manager.ImportFromJsonAsync<User>("users_export.json");
```

---

### Serialization Options

PersistX ships with three serializers. Swap them to trade readability for performance.

```csharp
// Default — JSON (human-readable, zero config, slightly larger files)
var col = await db.CreateCollectionAsync<User>("users");

// MessagePack — 5–10× faster serialization, much smaller files
using PersistX.Serialization;
var col = await db.CreateCollectionAsync<User>("users",
    serializer: new MessagePackSerializer<List<User>>());

// GZip-compressed JSON — best file size, moderate speed
var col = await db.CreateCollectionAsync<User>("users",
    serializer: new CompressedJsonSerializer<List<User>>());
```

| Serializer | Speed | File Size | Human-Readable |
|---|:---:|:---:|:---:|
| `JsonSerializer` | Good | Medium | ✅ Yes |
| `MessagePackSerializer` | Excellent | Small | ❌ Binary |
| `CompressedJsonSerializer` | Moderate | Smallest | ❌ Compressed |

---

### Persistent Indexes — Survive Restarts

`HashIndex` is fast but in-memory only (wiped on restart). `PersistentHashIndex` saves its data to your storage backend:

```csharp
using PersistX.Indexes;

var index = new PersistentHashIndex<string, User>(
    name:        "by_email",
    keySelector: u => u.Email,
    backend:     myBackend);

await index.InitializeAsync(); // loads from "{name}.hidx" on the backend if it exists

await index.AddAsync("alice@example.com", alice);

// Index data is persisted — next InitializeAsync() reloads it automatically
// No need to rebuild indexes from scratch on every startup
```

---

### Write-Ahead Log — Crash-Safe Durability

For scenarios where data loss on crash is unacceptable, wrap your backend with `WalBackend`:

```csharp
using PersistX.Storage;

var walManager = new WalManager("./data/myapp.wal");
var walBackend  = new WalBackend(fileBackend, walManager);

// On startup: replay any uncommitted transactions from before the crash
await walBackend.RecoverAsync();

// All transactional writes are journaled before being applied
var txId = Guid.NewGuid();
walBackend.BeginTransaction(txId);
await walBackend.WriteAsync("users.data", 0, userData);
await walBackend.WriteAsync("orders.data", 0, orderData);
await walBackend.CommitTransactionAsync(txId); // atomic — all or nothing
```

---

### Dependency Injection

Register PersistX with your DI container in `Program.cs`:

```csharp
using PersistX.Extensions;

// One-liner with defaults
builder.Services.AddPersistX(options =>
{
    options.DatabaseName = "MyApp";
    options.FilePath     = "./data";
    options.Backend      = PersistXBackendType.File;
});

// Or pick a specific backend
builder.Services.AddPersistXFileDatabase("MyApp", "./data/app.db");
builder.Services.AddPersistXInMemoryDatabase("TestDb");   // great for integration tests
builder.Services.AddPersistXSQLiteDatabase("MyApp", "Data Source=app.db");

// Inject IDatabase anywhere
public class UserService(IDatabase db)
{
    public async Task<User?> FindByEmailAsync(string email)
    {
        var users = await db.GetCollectionAsync<User>("users");
        if (users is null) return null;

        var idx = await users.GetIndexAsync<string>("by_email");
        return idx is not null
            ? await idx.FindAsync(email).FirstOrDefaultAsync()
            : await users.FirstOrDefaultAsync(u => u.Email == email);
    }
}
```

---

## API Reference

### File-Based Collections

| Class | Description |
|---|---|
| `PersistentList<T>` | Ordered list. Full CRUD + sorting + slicing. |
| `PersistentDictionary<TKey, TValue>` | Key-value store. |
| `PersistentSet<T>` | Unique value set. |
| `PersistentQueue<T>` | FIFO queue. `Enqueue` / `Dequeue` / `Peek`. |
| `PersistentStack<T>` | LIFO stack. `Push` / `Pop` / `Peek`. |

### Database

| Class / Interface | Description |
|---|---|
| `IDatabase` | Main database interface. Create, get, drop collections. |
| `DatabaseFactory` | Creates `IDatabase` instances (File, SQLite, Memory). |
| `IPersistentCollection<T>` | Full CRUD + indexing + statistics. |

### Indexes

| Class | Type | Range Queries | Persistent |
|---|---|:---:|:---:|
| `HashIndex<TKey, TValue>` | In-memory hash | O(n) scan | ❌ |
| `BTreeIndex<TKey, TValue>` | In-memory B+ tree | O(log n + k) | ❌ |
| `PersistentHashIndex<TKey, TValue>` | File-backed hash | O(n) scan | ✅ |

### Serializers

| Class | Format | Best For |
|---|---|---|
| `JsonSerializer<T>` | JSON | Default, debugging, human-readable files |
| `MessagePackSerializer<T>` | MessagePack binary | High throughput, small footprint |
| `CompressedJsonSerializer<T>` | GZip + JSON | Large payloads, disk space optimization |

### Advanced

| Class | Namespace | Description |
|---|---|---|
| `TtlCollection<T>` | `PersistX.Expiry` | Auto-expiring items with TTL |
| `ObservableCollectionDecorator<T>` | `PersistX.Events` | Change notifications via Channel |
| `SnapshotManager` | `PersistX.Backup` | ZIP snapshots + CSV/JSON export |
| `WalManager` | `PersistX.Storage` | Write-ahead log management |
| `WalBackend` | `PersistX.Storage` | Crash-safe transactional backend |

---

## Project Structure

```
PersistX/
├── FileBased/          PersistentList, Dictionary, Set, Queue, Stack
├── Database/           Database engine, TransactionManager, DatabaseFactory
├── Collections/        PersistentCollection<T> (database-integrated)
├── Indexes/            HashIndex, BTreeIndex, PersistentHashIndex
├── Storage/            FileStorage, MemoryStorage, SQLiteStorage, WalBackend
├── Serialization/      JsonSerializer, MessagePackSerializer, CompressedJsonSerializer
├── Extensions/         LINQ extensions (CollectionExtensions), DI helpers
├── Expiry/             TtlCollection, TtlItem
├── Events/             CollectionChange, ObservableCollectionDecorator
└── Backup/             SnapshotManager, SnapshotManifest
```

---

## Running the Tests

```bash
# Run all 130 unit tests
dotnet test src/PersistX.UnitTests

# Run the demo/examples console app
dotnet run --project src/PersistX.Test
```

---

## Documentation

- [EXAMPLES.md](EXAMPLES.md) — Detailed real-world examples
- [ROADMAP.md](ROADMAP.md) — Upcoming features
- [CHANGELOG.md](CHANGELOG.md) — Version history and breaking changes

---

## Contributing

All contributions are welcome — bug fixes, new features, tests, and documentation.

1. Fork the repository
2. Create a feature branch
3. Make your changes with tests
4. Open a pull request

Please open an issue first for large changes so we can discuss the approach.

---

## License

MIT — free for personal and commercial use. See [LICENSE](LICENSE).

---

## Support the Project

If PersistX saves you time, consider [supporting development](Donation.md). Every contribution helps keep the project maintained and growing.
