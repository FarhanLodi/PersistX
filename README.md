<div align="center">
  <img src="assets/PersistX.png" alt="PersistX Logo" width="200"/>
  
  # PersistX
  
  A high-performance persistent collection library for .NET with ACID transactions, indexing, and advanced data structures.
  
  [![NuGet](https://img.shields.io/nuget/v/PersistX.svg)](https://www.nuget.org/packages/PersistX/1.0.0)
  [![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
</div>

## 🎯 Overview

PersistX is a next-generation replacement for simple file-based persistent collections. Unlike existing libraries, PersistX supports scalability, performance, transactions, indexing, async APIs, and advanced collection types, making it feel like a mini embedded database engine but with collection-first abstractions.

## ✨ Features

### ✅ Implemented (v1.0.0)
- **File-Based Collections**: Easy-to-use persistent collections (List, Dictionary, Set)
- **Database Collections**: Enterprise-grade collections with advanced features
- **Storage Backends**: File, In-Memory, and SQLite storage
- **Transaction Support**: ACID transactions with savepoints
- **Indexing**: Hash-based indexes for fast lookups
- **Serialization**: JSON serialization support
- **Async APIs**: Full async/await support throughout
- **Batch Operations**: High-performance bulk operations
- **Performance Optimized**: Optimized for large datasets

## 🏗️ Project Structure

```
PersistX/
├── FileBased/        # File-based, standalone collections
│   ├── PersistentList.cs
│   ├── PersistentDictionary.cs
│   └── PersistentSet.cs
├── Database/         # Database operations and management
│   ├── Database.cs
│   ├── DatabaseFactory.cs
│   └── TransactionManager.cs
├── Collections/      # Database-integrated collections
│   └── PersistentCollection.cs
├── Indexes/          # Indexing system
│   └── HashIndex.cs
├── Storage/          # Storage backends
│   ├── FileStorage.cs
│   ├── MemoryStorage.cs
│   └── SQLiteStorage.cs
├── Serialization/    # Serialization
│   └── JsonSerializer.cs
└── Interfaces/       # Core interfaces
    ├── IPersistentCollection.cs
    ├── IDatabase.cs
    ├── IBackend.cs
    ├── IIndex.cs
    └── ISerializer.cs
```

## 🚀 Quick Start

### 📦 Installation

#### Package Manager Console
```powershell
Install-Package PersistX
```

#### .NET CLI
```bash
dotnet add package PersistX
```

#### PackageReference (csproj)
```xml
<PackageReference Include="PersistX" Version="1.0.0" />
```

#### Direct Download
[![Download from NuGet](https://img.shields.io/nuget/dt/PersistX.svg?label=Downloads)](https://www.nuget.org/packages/PersistX/)
[Download from NuGet.org](https://www.nuget.org/packages/PersistX/)

### File-Based Collections (Quick & Easy)

```csharp
using PersistX.FileBased;

// Create a persistent list
var tasks = new PersistentList<string>("tasks.json");
await tasks.AddAsync("Complete project");
await tasks.AddAsync("Review code");

// Create a persistent dictionary
var settings = new PersistentDictionary<string, object>("settings.json");
await settings.SetAsync("theme", "dark");
await settings.SetAsync("notifications", true);

// Create a persistent set
var tags = new PersistentSet<string>("tags.json");
await tags.AddAsync("work");
await tags.AddAsync("urgent");
```

### Database Collections (Enterprise Features)

```csharp
using PersistX.Database;
using PersistX.Collections;

// Create a database
var database = await DatabaseFactory.CreateFileDatabaseAsync("MyApp", "app.db");

// Create a collection with indexes
var users = await database.CreateCollectionAsync<User>("users");
await users.CreateIndexAsync("email_index", u => u.Email);
await users.CreateIndexAsync("name_index", u => u.Name);

// Add users with transactions
await database.ExecuteInTransactionAsync(async transaction =>
{
    await users.AddAsync(new User { Name = "John", Email = "john@example.com" });
    await users.AddAsync(new User { Name = "Jane", Email = "jane@example.com" });
});

// Search using indexes
var emailIndex = await users.GetIndexAsync<string>("email_index");
await foreach (var user in emailIndex.FindAsync("john@example.com"))
{
    Console.WriteLine($"Found: {user.Name}");
}
```

## 📁 File Storage

### File-Based Collections
- **Default Path**: `./persistx_data/` (relative to application)
- **Custom Path**: Specify full path in constructor
- **File Format**: JSON files (`.json`)

### Database Collections
- **Default Path**: `./persistx_data/` (relative to application)
- **Custom Path**: Specify full path in `DatabaseFactory.CreateFileDatabaseAsync()`
- **File Format**: Database files (`.db`) with metadata

### Example Paths
```csharp
// File-based collections
var list = new PersistentList<string>("C:/MyApp/data/tasks.json");

// Database collections
var db = await DatabaseFactory.CreateFileDatabaseAsync("MyApp", "C:/MyApp/data/app.db");
```

## 🧪 Testing & Examples

Run the test console application to see all features in action:

```bash
cd src/PersistX.Test
dotnet run
```

The test application includes:
- **File-Based Collections Demo**: Basic operations and file management
- **Database Collections Demo**: Advanced features, transactions, and indexing
- **Performance Tests**: Bulk operations and performance benchmarks
- **Real-World Examples**: Shopping cart, blog system, inventory management

## 📚 Documentation

- **[Detailed Examples](EXAMPLES.md)**: Comprehensive usage examples and patterns
- **[Development Roadmap](ROADMAP.md)**: Future features and development plans

## 📦 NuGet Package

```xml
<PackageReference Include="PersistX" Version="1.0.0" />
```

### Package Structure
- **PersistX**: Core library with all features
- **PersistX.FileBased**: File-based collections only (lightweight)
- **PersistX.Database**: Database collections only (enterprise)

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with .NET 9.0
- Inspired by modern database design principles
- Community feedback and contributions