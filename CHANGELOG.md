# Changelog

All notable changes to PersistX will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


## [1.0.1] - 2025-09-09

### 🚀 Major Release - Advanced Storage Features

This release introduces enterprise-grade storage features that make PersistX suitable for production applications requiring data durability, security, and performance.

### Added

#### **Write-Ahead Logging (WAL)** 📝
- **Crash Recovery**: Automatic recovery from system crashes and unexpected shutdowns
- **Data Durability**: Guaranteed persistence of committed changes
- **Transaction Logging**: All operations logged before being applied to storage
- **Atomic Operations**: All-or-nothing transaction semantics
- **Performance**: Sequential log writes for optimal I/O performance
- **Configuration**: Configurable WAL file size limits and storage paths

#### **Data Compression** 🗜️
- **GZip Compression**: High compression ratio for text data (50-80% savings)
- **Deflate Compression**: Fast compression for binary data
- **Automatic Compression**: Transparent compression/decompression
- **Storage Efficiency**: Significant reduction in disk usage
- **Performance**: Minimal impact on application performance

#### **Encryption at Rest** 🔐
- **AES-256 Encryption**: Military-grade encryption standard
- **User-Provided Keys**: You control your encryption keys (no auto-generation)
- **Automatic Encryption**: Data encrypted before storage
- **Secure Key Management**: Keys never stored in plain text
- **Key Generation Utilities**: Helper methods to generate and validate keys
- **Compliance**: Meet regulatory requirements (GDPR, HIPAA)
- **Transparent**: No code changes required for existing applications

#### **Automated Backup & Restore** 💾
- **Scheduled Backups**: Automatic backup creation
- **Incremental Backups**: Only backup changed data
- **Point-in-Time Recovery**: Restore to any previous state
- **Backup Verification**: Ensure backup integrity
- **Metadata Tracking**: Comprehensive backup metadata and statistics

#### **Memory Mapping** 🗺️
- **Memory-Mapped Files**: Direct memory access to file data
- **Automatic Fallback**: Falls back to FileStream for reliability
- **Large File Support**: Efficient handling of large datasets
- **Performance**: Optimal I/O performance for large files
- **Reliability**: Automatic fallback ensures data integrity

#### **Storage Statistics** 📊
- **Performance Metrics**: Monitor read/write performance
- **Storage Usage**: Track disk space consumption
- **Index Statistics**: Analyze index usage and effectiveness
- **Compression Ratios**: Monitor compression effectiveness
- **WAL Statistics**: Track WAL size and operation counts

### Changed
- **Enhanced Database Configuration**: New configuration options for advanced features
- **Improved Error Handling**: Better error messages and recovery mechanisms
- **Performance Optimizations**: Faster operations with new storage features
- **API Enhancements**: New methods for monitoring and statistics

### Fixed
- **Memory Leaks**: Fixed memory leaks in long-running applications
- **Concurrency Issues**: Improved thread safety in high-concurrency scenarios
- **File Locking**: Better file locking mechanisms for multi-process access
- **Error Recovery**: Enhanced error recovery and data consistency

### Technical Details

#### **Write-Ahead Logging Implementation**
- Binary log format for efficient storage
- Transaction-based logging with commit/rollback markers
- Automatic replay on startup for crash recovery
- Configurable log file size limits
- Thread-safe logging operations

#### **Compression Integration**
- Transparent compression during serialization
- Configurable compression algorithms (GZip, Deflate)
- Automatic decompression during deserialization
- Compression ratio monitoring and statistics

#### **Encryption Implementation**
- AES-256-CBC encryption for data at rest
- User-provided encryption keys (no auto-generation)
- Secure key validation and management
- Encrypted metadata and index files
- Performance-optimized encryption/decryption
- Key generation utilities for secure key creation

#### **Backup System**
- File-based backup with metadata tracking
- Incremental backup support
- Backup verification and integrity checks
- Restore functionality with rollback capabilities

## [1.0.0] - 2025-09-08

### 🎉 Initial Release - Core Foundation

This is the first stable release of PersistX, providing a solid foundation for persistent collections with enterprise-grade features.

### Added

#### **File-Based Collections** 📁
- **PersistentList<T>**: Ordered collection with automatic persistence
- **PersistentDictionary<TKey, TValue>**: Key-value storage with automatic persistence
- **PersistentSet<T>**: Unique value collection with automatic persistence
- **Zero Configuration**: Just specify a file path and start using
- **Automatic Persistence**: Changes saved immediately to disk
- **Thread-Safe**: Safe for concurrent access from multiple threads

#### **Database Collections** 🗄️
- **PersistentCollection<T>**: Full-featured collection with database capabilities
- **ACID Transactions**: Full transaction support with rollback capabilities
- **Savepoints**: Create intermediate rollback points within transactions
- **Transaction Isolation**: Configurable isolation levels
- **Nested Transactions**: Support for complex transaction scenarios

#### **Storage Backends** 💾
- **FileStorage**: Direct file-based storage (default)
- **MemoryStorage**: In-memory storage for testing and caching
- **SQLiteStorage**: SQLite-based storage for complex queries
- **Configurable Backends**: Easy switching between storage types

#### **Indexing System** 🚀
- **Hash Indexes**: O(1) lookups on indexed properties
- **Multiple Indexes**: Index multiple properties simultaneously
- **Automatic Index Updates**: Indexes updated automatically with data changes
- **Index Statistics**: Monitor index usage and performance

#### **Serialization** 📄
- **JSON Serialization**: Built-in JSON serialization support
- **Custom Serializers**: Support for custom serialization logic
- **Type Safety**: Strong typing throughout the API
- **Performance**: Optimized serialization for large datasets

#### **Async APIs** ⚡
- **Full Async Support**: All operations support async/await
- **Non-Blocking**: No blocking operations in the API
- **Cancellation Support**: CancellationToken support throughout
- **Performance**: Optimized for high-throughput scenarios

#### **Batch Operations** 📦
- **Bulk Inserts**: High-performance bulk insert operations
- **Batch Updates**: Efficient batch update operations
- **Batch Deletes**: Fast bulk delete operations
- **Transaction Batching**: Batch operations within transactions

#### **Performance Optimization** 🏃‍♂️
- **Memory Efficient**: Optimized memory usage for large datasets
- **Fast I/O**: Optimized file I/O operations
- **Caching**: Intelligent caching of frequently accessed data
- **Lazy Loading**: Load data only when needed

### Technical Implementation

#### **Architecture**
- Clean separation of concerns with interface-based design
- Pluggable storage backends
- Extensible serialization system
- Modular indexing system

#### **Performance**
- Sub-millisecond operations for small datasets
- Optimized for datasets with millions of items
- Memory usage under 100MB for 1M items
- Storage overhead less than 2x compared to raw data

#### **Reliability**
- ACID transaction guarantees
- Thread-safe operations
- Error handling and recovery
- Data consistency validation

#### **Developer Experience**
- Intuitive API design
- Comprehensive documentation
- Extensive examples and demos
- Full .NET 9.0 support

### Package Structure
- **PersistX**: Complete library with all features
- **PersistX.FileBased**: File-based collections only (lightweight)
- **PersistX.Database**: Database collections only (enterprise)

### Documentation
- Comprehensive README with examples
- Detailed API documentation
- Performance benchmarks
- Real-world usage examples

---

## Version History Summary

| Version | Release Date | Key Features |
|---------|--------------|--------------|
| 1.0.1 | 2025-09-09 | Write-Ahead Logging, Compression, Encryption, Backup & Restore, Page-Based Storage |
| 1.0.0 | 2025-09-08 | Core Collections, Transactions, Indexing, Storage Backends, Async APIs |

## Migration Guide

### From 1.0.0 to 2.0.0

The 2.0.0 release is fully backward compatible with 1.0.0. Existing code will continue to work without changes. New advanced features are opt-in through configuration.

#### Enabling Advanced Features

```csharp
// Existing code continues to work
var database = await DatabaseFactory.CreateFileDatabaseAsync("MyApp", "app.db");

// To enable advanced features, use the new configuration approach
var config = new DatabaseConfiguration
{
    BackendConfiguration = new Dictionary<string, string>
    {
        ["FilePath"] = "app.db",
        ["EnableWAL"] = "true",           // Enable crash recovery
        ["CompressionType"] = "GZip",     // Enable compression
        ["EncryptionType"] = "Aes",       // Enable encryption
        ["EnableBackup"] = "true"         // Enable backups
    }
};

var database = new Database("MyApp", new FileStorage(), config);
await database.InitializeAsync();
```

#### New APIs Available

- `GetComprehensiveStatisticsAsync()` - Get detailed storage statistics
- `CreateBackupAsync()` - Create manual backups
- `ListBackupsAsync()` - List available backups
- `RestoreFromBackupAsync()` - Restore from backup
- `GetWalSizeAsync()` - Monitor WAL size
- `FlushWalAsync()` - Force WAL flush

## Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

## Support

- **Documentation**: [README.md](README.md)
- **Examples**: [EXAMPLES.md](EXAMPLES.md)
- **Roadmap**: [ROADMAP.md](ROADMAP.md)
- **Issues**: [GitHub Issues](https://github.com/your-repo/persistx/issues)
- **Discussions**: [GitHub Discussions](https://github.com/your-repo/persistx/discussions)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
