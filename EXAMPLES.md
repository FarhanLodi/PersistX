# PersistX - Comprehensive Usage Examples

This document provides detailed examples and patterns for using PersistX in various scenarios.

## 📚 Table of Contents

1. [Simple Collections Examples](#simple-collections-examples)
2. [Database Collections Examples](#database-collections-examples)
3. [Performance Optimization](#performance-optimization)
4. [Real-World Applications](#real-world-applications)

## Simple Collections Examples

### 1. Task Management System

```csharp
using PersistX.FileBased;

// Create task management system
var tasks = new PersistentList<string>("tasks.json");
var categories = new PersistentSet<string>("categories.json");
var settings = new PersistentDictionary<string, object>("settings.json");

// Initialize settings
await settings.SetAsync("theme", "light");
await settings.SetAsync("auto_save", true);
await settings.SetAsync("max_tasks", 100);

// Add categories
await categories.AddAsync("work");
await categories.AddAsync("personal");
await categories.AddAsync("urgent");

// Add tasks
await tasks.AddAsync("Complete project proposal");
await tasks.AddAsync("Review code changes");
await tasks.AddAsync("Update documentation");

// Batch operations for better performance
var newTasks = new[] { "Test application", "Deploy to production" };
await tasks.AddRangeAsync(newTasks);

Console.WriteLine($"Total tasks: {await tasks.CountAsync()}");
Console.WriteLine($"Categories: {string.Join(", ", await categories.GetAllAsync())}");
```

### 2. Application Settings Management

```csharp
using PersistX.FileBased;

// Create settings manager
var config = new PersistentDictionary<string, object>("app_config.json");

// Database settings
await config.SetAsync("database.connection_string", "Server=localhost;Database=MyApp;Trusted_Connection=true;");
await config.SetAsync("database.timeout", 30);
await config.SetAsync("database.pool_size", 10);

// API settings
await config.SetAsync("api.base_url", "https://api.example.com");
await config.SetAsync("api.timeout", 5000);
await config.SetAsync("api.retry_count", 3);

// Feature flags
await config.SetAsync("features.new_ui", true);
await config.SetAsync("features.beta_features", false);
await config.SetAsync("features.analytics", true);

// Retrieve settings
var connectionString = await config.GetAsync<string>("database.connection_string");
var apiTimeout = await config.GetAsync<int>("api.timeout");
var newUiEnabled = await config.GetAsync<bool>("features.new_ui");

Console.WriteLine($"Database: {connectionString}");
Console.WriteLine($"API Timeout: {apiTimeout}ms");
Console.WriteLine($"New UI: {newUiEnabled}");
```

### 3. Tag Management System

```csharp
using PersistX.FileBased;

// Create tag management system
var allTags = new PersistentSet<string>("all_tags.json");
var importantTags = new PersistentSet<string>("important_tags.json");

// Add tags to main collection
await allTags.AddAsync("work");
await allTags.AddAsync("personal");
await allTags.AddAsync("urgent");
await allTags.AddAsync("important");
await allTags.AddAsync("project");
await allTags.AddAsync("meeting");

// Add important tags
await importantTags.AddAsync("urgent");
await importantTags.AddAsync("important");
await importantTags.AddAsync("project");

// Check tag relationships
var isUrgentImportant = await importantTags.ContainsAsync("urgent");
var allImportantTags = await importantTags.GetAllAsync();

Console.WriteLine($"Urgent is important: {isUrgentImportant}");
Console.WriteLine($"Important tags: {string.Join(", ", allImportantTags)}");
```

## Database Collections Examples

### 1. User Management System

```csharp
using PersistX.Database;
using PersistX.Collections;

// Create user management database
var database = await DatabaseFactory.CreateFileDatabaseAsync("UserManagement", "user_management.db");

// Create collections with indexes
var users = await database.CreateCollectionAsync<User>("users");
var sessions = await database.CreateCollectionAsync<UserSession>("sessions");

// Create indexes for fast lookups
await users.CreateIndexAsync("email_index", u => u.Email);
await users.CreateIndexAsync("name_index", u => u.Name);
await sessions.CreateIndexAsync("user_id_index", s => s.UserId);

// Add users with transaction safety
await database.ExecuteInTransactionAsync(async transaction =>
{
    var newUsers = new[]
    {
        new User { Id = 1, Name = "Alice Johnson", Email = "alice@company.com", Role = "Manager", Department = "Engineering" },
        new User { Id = 2, Name = "Bob Smith", Email = "bob@company.com", Role = "Developer", Department = "Engineering" },
        new User { Id = 3, Name = "Carol Davis", Email = "carol@company.com", Role = "Designer", Department = "Design" }
    };

    foreach (var user in newUsers)
    {
        await users.AddAsync(user);
    }
});

// Search users by email
var emailIndex = await users.GetIndexAsync<string>("email_index");
await foreach (var user in emailIndex.FindAsync("alice@company.com"))
{
    Console.WriteLine($"Found user: {user.Name} ({user.Role})");
}
```

### 2. Product Catalog with Indexing

```csharp
using PersistX.Database;
using PersistX.Collections;

// Create product catalog database
var database = await DatabaseFactory.CreateFileDatabaseAsync("ProductCatalog", "product_catalog.db");
var products = await database.CreateCollectionAsync<Product>("products");

// Create multiple indexes for different search patterns
await products.CreateIndexAsync("category_index", p => p.Category);
await products.CreateIndexAsync("brand_index", p => p.Brand);
await products.CreateIndexAsync("price_index", p => p.Price);

// Add products
await database.ExecuteInTransactionAsync(async transaction =>
{
    var newProducts = new[]
    {
        new Product { Id = 1, Name = "Gaming Laptop", Category = "Electronics", Brand = "TechCorp", Price = 1299.99m, Stock = 50 },
        new Product { Id = 2, Name = "Wireless Mouse", Category = "Electronics", Brand = "TechCorp", Price = 29.99m, Stock = 200 },
        new Product { Id = 3, Name = "Programming Book", Category = "Books", Brand = "TechPress", Price = 49.99m, Stock = 100 }
    };

    foreach (var product in newProducts)
    {
        await products.AddAsync(product);
    }
});

// Search by category
var categoryIndex = await products.GetIndexAsync<string>("category_index");
await foreach (var product in categoryIndex.FindAsync("Electronics"))
{
    Console.WriteLine($"Electronics: {product.Name} - ${product.Price}");
}

// Search by brand
var brandIndex = await products.GetIndexAsync<string>("brand_index");
await foreach (var product in brandIndex.FindAsync("TechCorp"))
{
    Console.WriteLine($"TechCorp: {product.Name} - ${product.Price}");
}
```

### 3. Banking System with ACID Transactions

```csharp
using PersistX.Database;
using PersistX.Collections;

// Create banking system database
var database = await DatabaseFactory.CreateFileDatabaseAsync("BankingSystem", "banking_system.db");

var accounts = await database.CreateCollectionAsync<BankAccount>("accounts");
var transactions = await database.CreateCollectionAsync<BankTransaction>("transactions");

// Create accounts
await database.ExecuteInTransactionAsync(async transaction =>
{
    var newAccounts = new[]
    {
        new BankAccount { Id = 1, AccountNumber = "ACC001", CustomerId = "CUST001", Balance = 1000.00m, AccountType = "Checking" },
        new BankAccount { Id = 2, AccountNumber = "ACC002", CustomerId = "CUST002", Balance = 500.00m, AccountType = "Savings" }
    };

    foreach (var account in newAccounts)
    {
        await accounts.AddAsync(account);
    }
});

// Simulate money transfer with ACID properties
try
{
    await database.ExecuteInTransactionAsync(async transaction =>
    {
        // Create transfer transaction
        var transferTransaction = new BankTransaction
        {
            Id = Guid.NewGuid().ToString(),
            FromAccount = "ACC001",
            ToAccount = "ACC002",
            Amount = 200.00m,
            Type = "TRANSFER",
            Timestamp = DateTime.UtcNow,
            Description = "Transfer to savings account"
        };

        await transactions.AddAsync(transferTransaction);
        
        // In a real implementation, you would update account balances here
        Console.WriteLine("Transfer completed successfully!");
    });
}
catch (Exception ex)
{
    Console.WriteLine($"Transfer failed: {ex.Message}");
    // Transaction is automatically rolled back
}
```

## Performance Optimization

### Batch Operations

Always use batch operations for better performance:

```csharp
// ❌ Slow - Individual operations
for (int i = 0; i < 1000; i++)
{
    await list.AddAsync($"Item {i}");
}

// ✅ Fast - Batch operation
var items = new List<string>();
for (int i = 0; i < 1000; i++)
{
    items.Add($"Item {i}");
}
await list.AddRangeAsync(items);
```

### Database Collections Performance

```csharp
// Create collection with indexes
var collection = await database.CreateCollectionAsync<Item>("items");
await collection.CreateIndexAsync("category_index", item => item.Category);

// Batch add items
var items = new List<Item>();
for (int i = 0; i < 10000; i++)
{
    items.Add(new Item { Id = i, Category = $"Category {i % 10}", Value = i * 1.5 });
}

await collection.AddRangeAsync(items); // Single file operation

// Fast searches using indexes
var categoryIndex = await collection.GetIndexAsync<string>("category_index");
await foreach (var item in categoryIndex.FindAsync("Category 1"))
{
    // Process found items
}
```

## Real-World Applications

### 1. Shopping Cart System

```csharp
using PersistX.Database;
using PersistX.Collections;

var database = await DatabaseFactory.CreateFileDatabaseAsync("ShoppingCart", "shopping_cart.db");

var products = await database.CreateCollectionAsync<ShoppingProduct>("products");
var cart = await database.CreateCollectionAsync<CartItem>("cart");
var orders = await database.CreateCollectionAsync<Order>("orders");

// Create indexes
await products.CreateIndexAsync("category_index", p => p.Category);
await cart.CreateIndexAsync("user_id_index", c => c.UserId);
await orders.CreateIndexAsync("user_id_index", o => o.UserId);

// Add products
await products.AddRangeAsync(new[]
{
    new ShoppingProduct { Id = 1, Name = "Laptop", Category = "Electronics", Price = 999.99m, Stock = 50 },
    new ShoppingProduct { Id = 2, Name = "Mouse", Category = "Electronics", Price = 29.99m, Stock = 200 }
});

// Add to cart
await cart.AddAsync(new CartItem { UserId = "user1", ProductId = 1, Quantity = 1 });

// Create order
await orders.AddAsync(new Order 
{ 
    Id = Guid.NewGuid().ToString(), 
    UserId = "user1", 
    Items = new[] { new OrderItem { ProductId = 1, Quantity = 1, Price = 999.99m } },
    TotalAmount = 999.99m,
    Status = "Pending"
});
```

### 2. Blog System

```csharp
using PersistX.Database;
using PersistX.Collections;

var database = await DatabaseFactory.CreateFileDatabaseAsync("BlogSystem", "blog_system.db");

var posts = await database.CreateCollectionAsync<BlogPost>("posts");
var comments = await database.CreateCollectionAsync<Comment>("comments");
var tags = await database.CreateCollectionAsync<Tag>("tags");

// Create indexes
await posts.CreateIndexAsync("author_index", p => p.Author);
await posts.CreateIndexAsync("status_index", p => p.IsPublished);
await comments.CreateIndexAsync("post_id_index", c => c.PostId);
await tags.CreateIndexAsync("name_index", t => t.Name);

// Add blog post
await posts.AddAsync(new BlogPost
{
    Id = 1,
    Title = "Getting Started with PersistX",
    Content = "PersistX is a powerful library...",
    Author = "John Doe",
    CreatedAt = DateTime.UtcNow,
    Tags = new[] { "tutorial", "persistx", "dotnet" },
    IsPublished = true
});

// Add comment
await comments.AddAsync(new Comment
{
    Id = 1,
    PostId = 1,
    Author = "Jane Smith",
    Content = "Great tutorial!",
    CreatedAt = DateTime.UtcNow
});
```

### 3. Inventory Management

```csharp
using PersistX.Database;
using PersistX.Collections;

var database = await DatabaseFactory.CreateFileDatabaseAsync("Inventory", "inventory.db");

var products = await database.CreateCollectionAsync<InventoryProduct>("products");
var suppliers = await database.CreateCollectionAsync<Supplier>("suppliers");
var transactions = await database.CreateCollectionAsync<InventoryTransaction>("transactions");

// Create indexes
await products.CreateIndexAsync("category_index", p => p.Category);
await products.CreateIndexAsync("supplier_id_index", p => p.SupplierId);
await transactions.CreateIndexAsync("product_id_index", t => t.ProductId);
await transactions.CreateIndexAsync("type_index", t => t.Type);

// Add supplier
await suppliers.AddAsync(new Supplier
{
    Id = 1,
    Name = "TechCorp Supplies",
    ContactEmail = "orders@techcorp.com",
    Phone = "555-0101"
});

// Add product
await products.AddAsync(new InventoryProduct
{
    Id = 1,
    Name = "Laptop",
    Category = "Electronics",
    SupplierId = 1,
    CurrentStock = 50,
    MinStock = 10,
    MaxStock = 100,
    UnitPrice = 999.99m
});

// Record transaction
await transactions.AddAsync(new InventoryTransaction
{
    Id = 1,
    ProductId = 1,
    Type = "IN",
    Quantity = 25,
    TransactionDate = DateTime.UtcNow,
    Notes = "Stock replenishment"
});
```

## Best Practices

1. **Use batch operations** for better performance
2. **Create indexes** for frequently searched fields
3. **Use transactions** for data consistency
4. **Choose appropriate storage backend** (File for persistence, Memory for speed)
5. **Handle exceptions** properly, especially in transactions
6. **Monitor performance** with the built-in performance tests

## File Organization

```
MyApp/
├── data/
│   ├── simple/
│   │   ├── tasks.json
│   │   ├── settings.json
│   │   └── tags.json
│   └── databases/
│       ├── user_management.db
│       ├── product_catalog.db
│       └── inventory.db
└── MyApp.exe
```

This organization keeps your data files organized and makes it easy to backup, migrate, or share data between applications.
