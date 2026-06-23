using PersistX.Storage;
using Xunit;

namespace PersistX.UnitTests.Storage;

public class WalManagerTests : IDisposable
{
    private readonly string _dir;
    private readonly string _walPath;

    public WalManagerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "persistx_wal_" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
        _walPath = Path.Combine(_dir, "test.wal");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private WalEntry MakeWriteEntry(Guid txId, string location = "data/test.bin")
        => new WalEntry
        {
            EntryId = Guid.NewGuid(),
            TransactionId = txId,
            OperationType = WalOperationType.Write,
            Location = location,
            Offset = 0,
            Data = new byte[] { 1, 2, 3 },
            Timestamp = DateTime.UtcNow
        };

    private WalEntry MakeCommitEntry(Guid txId)
        => new WalEntry
        {
            EntryId = Guid.NewGuid(),
            TransactionId = txId,
            OperationType = WalOperationType.Commit,
            Location = string.Empty,
            Offset = 0,
            Data = null,
            Timestamp = DateTime.UtcNow
        };

    private WalEntry MakeDeleteEntry(Guid txId, string location = "data/old.bin")
        => new WalEntry
        {
            EntryId = Guid.NewGuid(),
            TransactionId = txId,
            OperationType = WalOperationType.Delete,
            Location = location,
            Offset = 0,
            Data = null,
            Timestamp = DateTime.UtcNow
        };

    [Fact]
    public async Task AppendAsync_ThenReadAllEntriesAsync_ReturnsTheEntry()
    {
        await using var wal = new WalManager(_walPath);
        var txId = Guid.NewGuid();
        var entry = MakeWriteEntry(txId);

        await wal.AppendAsync(entry);

        var entries = await wal.ReadAllEntriesAsync();
        Assert.Single(entries);
        Assert.Equal(entry.EntryId, entries[0].EntryId);
        Assert.Equal(txId, entries[0].TransactionId);
    }

    [Fact]
    public async Task MultipleEntries_ReadTransactionEntriesAsync_FiltersByTxId()
    {
        await using var wal = new WalManager(_walPath);
        var tx1 = Guid.NewGuid();
        var tx2 = Guid.NewGuid();

        await wal.AppendAsync(MakeWriteEntry(tx1, "loc/a"));
        await wal.AppendAsync(MakeWriteEntry(tx2, "loc/b"));
        await wal.AppendAsync(MakeWriteEntry(tx1, "loc/c"));

        var tx1Entries = await wal.ReadTransactionEntriesAsync(tx1);
        Assert.Equal(2, tx1Entries.Count);
        Assert.All(tx1Entries, e => Assert.Equal(tx1, e.TransactionId));

        var tx2Entries = await wal.ReadTransactionEntriesAsync(tx2);
        Assert.Single(tx2Entries);
        Assert.Equal(tx2, tx2Entries[0].TransactionId);
    }

    [Fact]
    public async Task HasUncommittedTransactionsAsync_TrueWhenWriteWithoutCommit()
    {
        await using var wal = new WalManager(_walPath);
        var txId = Guid.NewGuid();

        await wal.AppendAsync(MakeWriteEntry(txId));

        var hasUncommitted = await wal.HasUncommittedTransactionsAsync();
        Assert.True(hasUncommitted);
    }

    [Fact]
    public async Task HasUncommittedTransactionsAsync_FalseAfterCommitEntry()
    {
        await using var wal = new WalManager(_walPath);
        var txId = Guid.NewGuid();

        await wal.AppendAsync(MakeWriteEntry(txId));
        await wal.AppendAsync(MakeCommitEntry(txId));

        var hasUncommitted = await wal.HasUncommittedTransactionsAsync();
        Assert.False(hasUncommitted);
    }

    [Fact]
    public async Task HasUncommittedTransactionsAsync_FalseOnEmptyWal()
    {
        await using var wal = new WalManager(_walPath);
        var hasUncommitted = await wal.HasUncommittedTransactionsAsync();
        Assert.False(hasUncommitted);
    }

    [Fact]
    public async Task TruncateCompletedAsync_RemovesCommittedTransactions()
    {
        await using var wal = new WalManager(_walPath);
        var committed = Guid.NewGuid();
        var pending = Guid.NewGuid();

        await wal.AppendAsync(MakeWriteEntry(committed, "data/committed"));
        await wal.AppendAsync(MakeCommitEntry(committed));
        await wal.AppendAsync(MakeWriteEntry(pending, "data/pending"));

        await wal.TruncateCompletedAsync();

        var remaining = await wal.ReadAllEntriesAsync();
        // Only the pending (uncommitted) write should remain
        Assert.Single(remaining);
        Assert.Equal(pending, remaining[0].TransactionId);
    }

    [Fact]
    public async Task TruncateCompletedAsync_AllCommitted_EmptyWal()
    {
        await using var wal = new WalManager(_walPath);
        var tx1 = Guid.NewGuid();
        var tx2 = Guid.NewGuid();

        await wal.AppendAsync(MakeWriteEntry(tx1));
        await wal.AppendAsync(MakeCommitEntry(tx1));
        await wal.AppendAsync(MakeWriteEntry(tx2));
        await wal.AppendAsync(MakeCommitEntry(tx2));

        await wal.TruncateCompletedAsync();

        var remaining = await wal.ReadAllEntriesAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task GetUncommittedTransactionIdsAsync_ReturnsCorrectTxIds()
    {
        await using var wal = new WalManager(_walPath);
        var tx1 = Guid.NewGuid();
        var tx2 = Guid.NewGuid();
        var tx3 = Guid.NewGuid();

        await wal.AppendAsync(MakeWriteEntry(tx1)); // uncommitted
        await wal.AppendAsync(MakeWriteEntry(tx2));
        await wal.AppendAsync(MakeCommitEntry(tx2)); // committed
        await wal.AppendAsync(MakeWriteEntry(tx3)); // uncommitted

        var uncommitted = await wal.GetUncommittedTransactionIdsAsync();

        Assert.Equal(2, uncommitted.Count);
        Assert.Contains(tx1, uncommitted);
        Assert.Contains(tx3, uncommitted);
        Assert.DoesNotContain(tx2, uncommitted);
    }

    [Fact]
    public async Task AppendAsync_MultipleEntries_AllReadBack()
    {
        await using var wal = new WalManager(_walPath);
        var txId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
            await wal.AppendAsync(MakeWriteEntry(txId, $"data/file{i}"));

        var entries = await wal.ReadAllEntriesAsync();
        Assert.Equal(5, entries.Count);
    }

    [Fact]
    public async Task DeleteEntry_IsConsideredWriteForUncommittedCheck()
    {
        await using var wal = new WalManager(_walPath);
        var txId = Guid.NewGuid();

        await wal.AppendAsync(MakeDeleteEntry(txId));

        var hasUncommitted = await wal.HasUncommittedTransactionsAsync();
        Assert.True(hasUncommitted);
    }
}
