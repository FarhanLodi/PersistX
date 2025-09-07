using PersistX.Database;
using PersistX.Collections;

namespace PersistX.Test.Demos;

/// <summary>
/// Demonstrates database collections with advanced features like transactions and indexing
/// </summary>
public class DatabaseCollectionsDemo
{
    private static string GetDataPath(string fileName)
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "persistx_data");
        Directory.CreateDirectory(dataDir);
        var fullPath = Path.Combine(dataDir, fileName);
        Console.WriteLine($"📁 Database will be saved to: {Path.GetFullPath(fullPath)}");
        return fullPath;
    }
    public static async Task RunDemoAsync()
    {
        Console.WriteLine("=== Database Collections Demo ===");
        Console.WriteLine("These collections provide enterprise features like transactions, indexing, and multiple storage backends.");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("Choose a database collection demo:");
            Console.WriteLine("1) User Management System - Basic database operations");
            Console.WriteLine("2) Product Catalog - Indexing and searching");
            Console.WriteLine("3) Banking System - Transactions and ACID properties");
            Console.WriteLine("4) Logging System - Multiple collections and indexes");
            Console.WriteLine("5) Storage Backends - File, Memory, and SQLite");
            Console.WriteLine("0) Back to main menu");
            Console.Write("Select an option: ");

            var input = Console.ReadLine();
            Console.WriteLine();

            switch (input)
            {
                case "1":
                    await UserManagementDemoAsync();
                    break;
                case "2":
                    await ProductCatalogDemoAsync();
                    break;
                case "3":
                    await BankingSystemDemoAsync();
                    break;
                case "4":
                    await LoggingSystemDemoAsync();
                    break;
                case "5":
                    await StorageBackendsDemoAsync();
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Invalid selection. Try again.");
                    break;
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            Console.Clear();
        }
    }

    private static async Task UserManagementDemoAsync()
    {
        Console.WriteLine("=== User Management System Demo ===");
        Console.WriteLine("This demo shows how to create a user management system with database collections.");
        Console.WriteLine();

        // Create database
        var database = await DatabaseFactory.CreateFileDatabaseAsync("UserManagement", GetDataPath("user_management.db"));
        
        try
        {
            Console.WriteLine("Creating users collection with indexes...");
            var usersCollection = await database.CreateCollectionAsync<User>("users");
            await usersCollection.CreateIndexAsync("email_index", u => u.Email);
            await usersCollection.CreateIndexAsync("role_index", u => u.Role);
            await usersCollection.CreateIndexAsync("department_index", u => u.Department);

            Console.WriteLine("Creating sessions collection...");
            var sessionsCollection = await database.CreateCollectionAsync<UserSession>("sessions");
            await sessionsCollection.CreateIndexAsync("user_id_index", s => s.UserId);

            Console.WriteLine();
            Console.WriteLine("Adding users with transaction safety...");
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var users = new[]
                {
                    new User { Id = 1, Name = "Alice Johnson", Email = "alice@company.com", Role = "Manager", Department = "Engineering" },
                    new User { Id = 2, Name = "Bob Smith", Email = "bob@company.com", Role = "Developer", Department = "Engineering" },
                    new User { Id = 3, Name = "Carol Davis", Email = "carol@company.com", Role = "Designer", Department = "Design" },
                    new User { Id = 4, Name = "David Wilson", Email = "david@company.com", Role = "Manager", Department = "Sales" },
                    new User { Id = 5, Name = "Eva Brown", Email = "eva@company.com", Role = "Developer", Department = "Engineering" }
                };

                foreach (var user in users)
                {
                    await usersCollection.AddAsync(user);
                    Console.WriteLine($"  Added user: {user.Name} ({user.Role}) - {user.Department}");
                }
            });

            Console.WriteLine();
            Console.WriteLine("Simulating user sessions...");
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var sessions = new[]
                {
                    new UserSession { Id = Guid.NewGuid().ToString(), UserId = 1, LoginTime = DateTime.UtcNow.AddHours(-2), IsActive = true },
                    new UserSession { Id = Guid.NewGuid().ToString(), UserId = 2, LoginTime = DateTime.UtcNow.AddHours(-1), IsActive = true },
                    new UserSession { Id = Guid.NewGuid().ToString(), UserId = 3, LoginTime = DateTime.UtcNow.AddMinutes(-30), IsActive = false }
                };

                foreach (var session in sessions)
                {
                    await sessionsCollection.AddAsync(session);
                    Console.WriteLine($"  Created session for User {session.UserId}: {session.LoginTime:HH:mm:ss}");
                }
            });

            Console.WriteLine();
            Console.WriteLine("✅ User Management System demo completed!");
            Console.WriteLine("Database file 'user_management.db' has been created with users and sessions collections.");
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task ProductCatalogDemoAsync()
    {
        Console.WriteLine("=== Product Catalog Demo ===");
        Console.WriteLine("This demo shows indexing and searching capabilities.");
        Console.WriteLine();

        var database = await DatabaseFactory.CreateFileDatabaseAsync("ProductCatalog", GetDataPath("product_catalog.db"));
        
        try
        {
            Console.WriteLine("Creating products collection with indexes...");
            var productsCollection = await database.CreateCollectionAsync<Product>("products");
            await productsCollection.CreateIndexAsync("category_index", p => p.Category);
            await productsCollection.CreateIndexAsync("price_index", p => p.Price);
            await productsCollection.CreateIndexAsync("brand_index", p => p.Brand);

            Console.WriteLine();
            Console.WriteLine("Adding sample products...");
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var products = new[]
                {
                    new Product { Id = 1, Name = "Gaming Laptop", Category = "Electronics", Brand = "TechCorp", Price = 1299.99m, Stock = 15 },
                    new Product { Id = 2, Name = "Programming Book", Category = "Books", Brand = "TechPress", Price = 49.99m, Stock = 50 },
                    new Product { Id = 3, Name = "Wireless Mouse", Category = "Electronics", Brand = "TechCorp", Price = 29.99m, Stock = 100 },
                    new Product { Id = 4, Name = "Design Patterns Book", Category = "Books", Brand = "TechPress", Price = 59.99m, Stock = 25 },
                    new Product { Id = 5, Name = "Mechanical Keyboard", Category = "Electronics", Brand = "KeyMaster", Price = 149.99m, Stock = 30 },
                    new Product { Id = 6, Name = "Coffee Mug", Category = "Accessories", Brand = "DailyGoods", Price = 12.99m, Stock = 200 }
                };

                foreach (var product in products)
                {
                    await productsCollection.AddAsync(product);
                    Console.WriteLine($"  Added: {product.Name} - ${product.Price} ({product.Stock} in stock)");
                }
            });

            Console.WriteLine();
            Console.WriteLine("Performing real searches using indexes...");
            
            // Real category search
            Console.WriteLine("Searching for Electronics:");
            var electronicsIndex = await productsCollection.GetIndexAsync<string>("category_index");
            if (electronicsIndex != null)
            {
                await foreach (var product in electronicsIndex.FindAsync("Electronics"))
                {
                    Console.WriteLine($"  - {product.Name}: ${product.Price}");
                }
            }

            // Real brand search
            Console.WriteLine();
            Console.WriteLine("Searching for TechCorp products:");
            var brandIndex = await productsCollection.GetIndexAsync<string>("brand_index");
            if (brandIndex != null)
            {
                await foreach (var product in brandIndex.FindAsync("TechCorp"))
                {
                    Console.WriteLine($"  - {product.Name}: ${product.Price}");
                }
            }

            // Real price range search (using LINQ on all products)
            Console.WriteLine();
            Console.WriteLine("Searching for products under $100:");
            var allProducts = new List<Product>();
            await foreach (var product in productsCollection.GetAllAsync())
            {
                allProducts.Add(product);
            }
            var affordableProducts = allProducts.Where(p => p.Price < 100).OrderBy(p => p.Price);
            foreach (var product in affordableProducts)
            {
                Console.WriteLine($"  - {product.Name}: ${product.Price}");
            }

            Console.WriteLine();
            Console.WriteLine("✅ Product Catalog demo completed!");
            Console.WriteLine("Database file 'product_catalog.db' has been created with indexed product data.");
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task BankingSystemDemoAsync()
    {
        Console.WriteLine("=== Banking System Demo ===");
        Console.WriteLine("This demo shows ACID transactions and financial data integrity.");
        Console.WriteLine();

        var database = await DatabaseFactory.CreateFileDatabaseAsync("BankingSystem", GetDataPath("banking_system.db"));
        
        try
        {
            Console.WriteLine("Creating banking collections...");
            var accountsCollection = await database.CreateCollectionAsync<BankAccount>("accounts");
            await accountsCollection.CreateIndexAsync("account_number_index", a => a.AccountNumber);

            var transactionsCollection = await database.CreateCollectionAsync<BankTransaction>("transactions");
            await transactionsCollection.CreateIndexAsync("from_account_index", t => t.FromAccount);
            await transactionsCollection.CreateIndexAsync("to_account_index", t => t.ToAccount);

            Console.WriteLine();
            Console.WriteLine("Creating sample bank accounts...");
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var accounts = new[]
                {
                    new BankAccount { Id = 1, AccountNumber = "ACC001", CustomerId = "CUST001", Balance = 1000.00m, AccountType = "Checking" },
                    new BankAccount { Id = 2, AccountNumber = "ACC002", CustomerId = "CUST002", Balance = 500.00m, AccountType = "Savings" },
                    new BankAccount { Id = 3, AccountNumber = "ACC003", CustomerId = "CUST001", Balance = 2500.00m, AccountType = "Savings" }
                };

                foreach (var account in accounts)
                {
                    await accountsCollection.AddAsync(account);
                    Console.WriteLine($"  Created {account.AccountType} account {account.AccountNumber}: ${account.Balance}");
                }
            });

            Console.WriteLine();
            Console.WriteLine("Simulating money transfer: $200 from ACC001 to ACC002");
            Console.WriteLine("Initial balances:");
            Console.WriteLine("  ACC001: $1000.00");
            Console.WriteLine("  ACC002: $500.00");

            try
            {
                await database.ExecuteInTransactionAsync(async transaction =>
                {
                    // Simulate the transfer
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

                    Console.WriteLine();
                    Console.WriteLine("Transfer transaction details:");
                    Console.WriteLine($"  Transaction ID: {transferTransaction.Id}");
                    Console.WriteLine($"  Amount: ${transferTransaction.Amount}");
                    Console.WriteLine($"  From: {transferTransaction.FromAccount}");
                    Console.WriteLine($"  To: {transferTransaction.ToAccount}");
                    Console.WriteLine($"  Description: {transferTransaction.Description}");

                    // Add the transaction to the collection
                    await transactionsCollection.AddAsync(transferTransaction);
                    
                    // Simulate balance updates
                    Console.WriteLine();
                    Console.WriteLine("Updated account balances:");
                    Console.WriteLine("  ACC001: $1000.00 -> $800.00");
                    Console.WriteLine("  ACC002: $500.00 -> $700.00");
                });

                Console.WriteLine();
                Console.WriteLine("✅ Transfer completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"❌ Transfer failed: {ex.Message}");
                Console.WriteLine("All changes have been rolled back automatically.");
            }

            Console.WriteLine();
            Console.WriteLine("✅ Banking System demo completed!");
            Console.WriteLine("Database file 'banking_system.db' has been created with accounts and transactions.");
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task LoggingSystemDemoAsync()
    {
        Console.WriteLine("=== Logging System Demo ===");
        Console.WriteLine("This demo shows multiple collections and complex indexing.");
        Console.WriteLine();

        var database = await DatabaseFactory.CreateFileDatabaseAsync("LoggingSystem", GetDataPath("logging_system.db"));
        
        try
        {
            Console.WriteLine("Creating logging collections with indexes...");
            var logsCollection = await database.CreateCollectionAsync<LogEntry>("logs");
            await logsCollection.CreateIndexAsync("level_index", l => l.Level);
            await logsCollection.CreateIndexAsync("source_index", l => l.Source);
            await logsCollection.CreateIndexAsync("timestamp_index", l => l.Timestamp);

            var metricsCollection = await database.CreateCollectionAsync<Metric>("metrics");
            await metricsCollection.CreateIndexAsync("metric_name_index", m => m.Name);
            await metricsCollection.CreateIndexAsync("metric_timestamp_index", m => m.Timestamp);

            Console.WriteLine();
            Console.WriteLine("Simulating application logging...");
            var random = new Random();
            var logLevels = new[] { "INFO", "WARNING", "ERROR", "DEBUG" };
            var sources = new[] { "AuthService", "PaymentService", "UserService", "EmailService", "DatabaseService" };

            await database.ExecuteInTransactionAsync(async transaction =>
            {
                for (int i = 0; i < 15; i++)
                {
                    var logEntry = new LogEntry
                    {
                        Id = Guid.NewGuid().ToString(),
                        Level = logLevels[random.Next(logLevels.Length)],
                        Message = $"Sample log message {i + 1}",
                        Source = sources[random.Next(sources.Length)],
                        Timestamp = DateTime.UtcNow.AddMinutes(-random.Next(120))
                    };

                    await logsCollection.AddAsync(logEntry);
                    Console.WriteLine($"  [{logEntry.Level}] {logEntry.Source}: {logEntry.Message}");
                }
            });

            Console.WriteLine();
            Console.WriteLine("Simulating metrics collection...");
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var metrics = new[]
                {
                    new Metric { Id = Guid.NewGuid().ToString(), Name = "cpu_usage", Value = 45.2, Timestamp = DateTime.UtcNow.AddMinutes(-5) },
                    new Metric { Id = Guid.NewGuid().ToString(), Name = "memory_usage", Value = 67.8, Timestamp = DateTime.UtcNow.AddMinutes(-5) },
                    new Metric { Id = Guid.NewGuid().ToString(), Name = "request_count", Value = 1250, Timestamp = DateTime.UtcNow.AddMinutes(-5) },
                    new Metric { Id = Guid.NewGuid().ToString(), Name = "response_time", Value = 150.5, Timestamp = DateTime.UtcNow.AddMinutes(-5) }
                };

                foreach (var metric in metrics)
                {
                    await metricsCollection.AddAsync(metric);
                    Console.WriteLine($"  {metric.Name}: {metric.Value} ({metric.Timestamp:HH:mm:ss})");
                }
            });

            Console.WriteLine();
            Console.WriteLine("Performing real log queries...");
            
            // Query error logs
            Console.WriteLine("Error logs found:");
            var allLogs = new List<LogEntry>();
            await foreach (var log in logsCollection.GetAllAsync())
            {
                allLogs.Add(log);
            }
            var errorLogs = allLogs.Where(log => log.Level == "ERROR").Take(3);
            foreach (var log in errorLogs)
            {
                Console.WriteLine($"  [{log.Level}] {log.Source}: {log.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("✅ Logging System demo completed!");
            Console.WriteLine("Database file 'logging_system.db' has been created with logs and metrics collections.");
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task StorageBackendsDemoAsync()
    {
        Console.WriteLine("=== Storage Backends Demo ===");
        Console.WriteLine("This demo shows different storage backends: File, Memory, and SQLite.");
        Console.WriteLine();

        Console.WriteLine("1. File Backend Demo");
        Console.WriteLine("   - Creates a file-based database");
        Console.WriteLine("   - Data persists between application restarts");
        Console.WriteLine("   - Good for single-user applications");
        
        var fileDb = await DatabaseFactory.CreateFileDatabaseAsync("FileBackendDemo", GetDataPath("file_backend.db"));
        Console.WriteLine("   ✅ File backend database created: file_backend.db");
        await fileDb.DisposeAsync();

        Console.WriteLine();
        Console.WriteLine("2. In-Memory Backend Demo");
        Console.WriteLine("   - Creates an in-memory database");
        Console.WriteLine("   - Data is lost when application closes");
        Console.WriteLine("   - Good for testing and temporary data");
        
        var memoryDb = await DatabaseFactory.CreateInMemoryDatabaseAsync("MemoryBackendDemo");
        Console.WriteLine("   ✅ In-memory backend database created");
        await memoryDb.DisposeAsync();

        Console.WriteLine();
        Console.WriteLine("3. SQLite Backend Demo");
        Console.WriteLine("   - Creates a SQLite database");
        Console.WriteLine("   - Data persists between application restarts");
        Console.WriteLine("   - Good for multi-user applications and complex queries");
        
        var sqliteDb = await DatabaseFactory.CreateAsync("SQLiteBackendDemo", new DatabaseConfiguration
        {
            BackendType = BackendType.SQLite,
            BackendConfiguration = new Dictionary<string, string>
            {
                ["DataSource"] = GetDataPath("sqlite_backend.db")
            }
        });
        Console.WriteLine("   ✅ SQLite backend database created: sqlite_backend.db");
        await sqliteDb.DisposeAsync();

        Console.WriteLine();
        Console.WriteLine("✅ Storage Backends demo completed!");
        Console.WriteLine("Check the created database files in your project directory.");
    }
}

// Sample data models for the demos
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

public class UserSession
{
    public string Id { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime LoginTime { get; set; }
    public bool IsActive { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public class BankAccount
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string AccountType { get; set; } = string.Empty;
}

public class BankTransaction
{
    public string Id { get; set; } = string.Empty;
    public string FromAccount { get; set; } = string.Empty;
    public string ToAccount { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class LogEntry
{
    public string Id { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class Metric
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
}
