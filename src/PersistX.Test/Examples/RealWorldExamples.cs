using PersistX.FileBased;
using PersistX.Database;
using PersistX.Collections;

namespace PersistX.Test.Examples;

/// <summary>
/// Real-world examples showing how to use PersistX in practical applications
/// </summary>
public class RealWorldExamples
{
    private static string GetDataPath(string fileName)
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "persistx_data");
        Directory.CreateDirectory(dataDir);
        var fullPath = Path.Combine(dataDir, fileName);
        Console.WriteLine($"📁 Data will be saved to: {Path.GetFullPath(fullPath)}");
        return fullPath;
    }
    public static async Task RunExamplesAsync()
    {
        Console.WriteLine("=== Real-World Examples ===");
        Console.WriteLine("These examples show how to use PersistX in practical applications.");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("Choose a real-world example:");
            Console.WriteLine("1) Todo Application - Simple task management");
            Console.WriteLine("2) Configuration Manager - Application settings");
            Console.WriteLine("3) User Preferences - User-specific data");
            Console.WriteLine("4) Shopping Cart - E-commerce functionality");
            Console.WriteLine("5) Blog System - Content management");
            Console.WriteLine("6) Inventory Management - Product tracking");
            Console.WriteLine("7) All Examples - Run all examples");
            Console.WriteLine("0) Back to main menu");
            Console.Write("Select an option: ");

            var input = Console.ReadLine();
            Console.WriteLine();

            switch (input)
            {
                case "1":
                    await TodoApplicationExampleAsync();
                    break;
                case "2":
                    await ConfigurationManagerExampleAsync();
                    break;
                case "3":
                    await UserPreferencesExampleAsync();
                    break;
                case "4":
                    await ShoppingCartExampleAsync();
                    break;
                case "5":
                    await BlogSystemExampleAsync();
                    break;
                case "6":
                    await InventoryManagementExampleAsync();
                    break;
                case "7":
                    await AllExamplesAsync();
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

    private static async Task TodoApplicationExampleAsync()
    {
        Console.WriteLine("=== Todo Application Example ===");
        Console.WriteLine("A simple todo application using PersistentList and PersistentDictionary.");
        Console.WriteLine();

        // Create todo list
        var todos = new PersistentList<TodoItem>(GetDataPath("todo_app.json"));
        var categories = new PersistentSet<string>(GetDataPath("todo_categories.json"));
        var settings = new PersistentDictionary<string, object>(GetDataPath("todo_settings.json"));

        Console.WriteLine("Setting up todo application...");
        
        // Initialize settings
        await settings.SetAsync("theme", "light");
        await settings.SetAsync("auto_save", true);
        await settings.SetAsync("show_completed", false);
        await settings.SetAsync("sort_by", "priority");

        // Add some categories
        await categories.AddAsync("Work");
        await categories.AddAsync("Personal");
        await categories.AddAsync("Shopping");
        await categories.AddAsync("Health");

        // Add some todos
        var todoItems = new[]
        {
            new TodoItem { Id = 1, Title = "Complete project proposal", Category = "Work", Priority = "High", DueDate = DateTime.Today.AddDays(1) },
            new TodoItem { Id = 2, Title = "Buy groceries", Category = "Shopping", Priority = "Medium", DueDate = DateTime.Today },
            new TodoItem { Id = 3, Title = "Go to gym", Category = "Health", Priority = "Low", DueDate = DateTime.Today.AddDays(2) },
            new TodoItem { Id = 4, Title = "Call mom", Category = "Personal", Priority = "Medium", DueDate = DateTime.Today.AddDays(3) },
            new TodoItem { Id = 5, Title = "Review code changes", Category = "Work", Priority = "High", DueDate = DateTime.Today }
        };

        foreach (var todo in todoItems)
        {
            await todos.AddAsync(todo);
        }

        Console.WriteLine("Todo Application Summary:");
        Console.WriteLine($"  Total todos: {await todos.CountAsync()}");
        Console.WriteLine($"  Categories: {await categories.CountAsync()}");
        Console.WriteLine($"  Settings: {await settings.CountAsync()}");
        Console.WriteLine();

        Console.WriteLine("Categories:");
        await foreach (var category in categories.GetAllAsync())
        {
            Console.WriteLine($"  - {category}");
        }

        Console.WriteLine();
        Console.WriteLine("Todo Items:");
        await foreach (var todo in todos.GetAllAsync())
        {
            Console.WriteLine($"  [{todo.Priority}] {todo.Title} ({todo.Category}) - Due: {todo.DueDate:MM/dd/yyyy}");
        }

        Console.WriteLine();
        Console.WriteLine("Application Settings:");
        await foreach (var setting in settings.GetAllAsync())
        {
            Console.WriteLine($"  {setting.Key}: {setting.Value}");
        }

        Console.WriteLine();
        Console.WriteLine("✅ Todo Application example completed!");
        Console.WriteLine("Files created: todo_app.json, todo_categories.json, todo_settings.json");
    }

    private static async Task ConfigurationManagerExampleAsync()
    {
        Console.WriteLine("=== Configuration Manager Example ===");
        Console.WriteLine("A configuration manager for application settings using PersistentDictionary.");
        Console.WriteLine();

        var config = new PersistentDictionary<string, object>(GetDataPath("app_config.json"));

        Console.WriteLine("Setting up application configuration...");

        // Database settings
        await config.SetAsync("database.connection_string", "Server=localhost;Database=MyApp;Trusted_Connection=true;");
        await config.SetAsync("database.timeout", 30);
        await config.SetAsync("database.pool_size", 100);

        // API settings
        await config.SetAsync("api.base_url", "https://api.myapp.com");
        await config.SetAsync("api.timeout", 5000);
        await config.SetAsync("api.retry_count", 3);
        await config.SetAsync("api.rate_limit", 1000);

        // Logging settings
        await config.SetAsync("logging.level", "Information");
        await config.SetAsync("logging.file_path", "logs/app.log");
        await config.SetAsync("logging.max_file_size", 10485760); // 10MB
        await config.SetAsync("logging.retain_days", 30);

        // Feature flags
        await config.SetAsync("features.new_ui", true);
        await config.SetAsync("features.beta_features", false);
        await config.SetAsync("features.analytics", true);

        Console.WriteLine("Configuration Summary:");
        Console.WriteLine($"  Total settings: {await config.CountAsync()}");
        Console.WriteLine();

        Console.WriteLine("Database Configuration:");
        Console.WriteLine($"  Connection String: {await config.GetAsync("database.connection_string")}");
        Console.WriteLine($"  Timeout: {await config.GetAsync("database.timeout")} seconds");
        Console.WriteLine($"  Pool Size: {await config.GetAsync("database.pool_size")}");

        Console.WriteLine();
        Console.WriteLine("API Configuration:");
        Console.WriteLine($"  Base URL: {await config.GetAsync("api.base_url")}");
        Console.WriteLine($"  Timeout: {await config.GetAsync("api.timeout")}ms");
        Console.WriteLine($"  Retry Count: {await config.GetAsync("api.retry_count")}");

        Console.WriteLine();
        Console.WriteLine("Feature Flags:");
        Console.WriteLine($"  New UI: {await config.GetAsync("features.new_ui")}");
        Console.WriteLine($"  Beta Features: {await config.GetAsync("features.beta_features")}");
        Console.WriteLine($"  Analytics: {await config.GetAsync("features.analytics")}");

        Console.WriteLine();
        Console.WriteLine("✅ Configuration Manager example completed!");
        Console.WriteLine("File created: app_config.json");
    }

    private static async Task UserPreferencesExampleAsync()
    {
        Console.WriteLine("=== User Preferences Example ===");
        Console.WriteLine("User-specific preferences using multiple collections.");
        Console.WriteLine();

        // Create user-specific collections
        var userSettings = new PersistentDictionary<string, object>(GetDataPath("user_preferences.json"));
        var bookmarks = new PersistentList<Bookmark>(GetDataPath("user_bookmarks.json"));
        var tags = new PersistentSet<string>(GetDataPath("user_tags.json"));

        Console.WriteLine("Setting up user preferences...");

        // User settings
        await userSettings.SetAsync("display_name", "John Doe");
        await userSettings.SetAsync("email", "john.doe@example.com");
        await userSettings.SetAsync("theme", "dark");
        await userSettings.SetAsync("language", "en-US");
        await userSettings.SetAsync("timezone", "UTC-5");
        await userSettings.SetAsync("notifications.email", true);
        await userSettings.SetAsync("notifications.push", false);
        await userSettings.SetAsync("privacy.public_profile", false);

        // Bookmarks
        var bookmarkItems = new[]
        {
            new Bookmark { Id = 1, Title = "PersistX Documentation", Url = "https://github.com/persistx/docs", Tags = new[] { "documentation", "persistx" } },
            new Bookmark { Id = 2, Title = "C# Best Practices", Url = "https://docs.microsoft.com/csharp", Tags = new[] { "csharp", "programming" } },
            new Bookmark { Id = 3, Title = "Database Design", Url = "https://example.com/db-design", Tags = new[] { "database", "design" } },
            new Bookmark { Id = 4, Title = "API Design", Url = "https://example.com/api-design", Tags = new[] { "api", "design" } }
        };

        foreach (var bookmark in bookmarkItems)
        {
            await bookmarks.AddAsync(bookmark);
        }

        // Tags
        await tags.AddAsync("documentation");
        await tags.AddAsync("persistx");
        await tags.AddAsync("csharp");
        await tags.AddAsync("programming");
        await tags.AddAsync("database");
        await tags.AddAsync("design");
        await tags.AddAsync("api");

        Console.WriteLine("User Preferences Summary:");
        Console.WriteLine($"  Settings: {await userSettings.CountAsync()}");
        Console.WriteLine($"  Bookmarks: {await bookmarks.CountAsync()}");
        Console.WriteLine($"  Tags: {await tags.CountAsync()}");
        Console.WriteLine();

        Console.WriteLine("User Profile:");
        Console.WriteLine($"  Name: {await userSettings.GetAsync("display_name")}");
        Console.WriteLine($"  Email: {await userSettings.GetAsync("email")}");
        Console.WriteLine($"  Theme: {await userSettings.GetAsync("theme")}");
        Console.WriteLine($"  Language: {await userSettings.GetAsync("language")}");

        Console.WriteLine();
        Console.WriteLine("Bookmarks:");
        await foreach (var bookmark in bookmarks.GetAllAsync())
        {
            Console.WriteLine($"  {bookmark.Title}");
            Console.WriteLine($"    URL: {bookmark.Url}");
            Console.WriteLine($"    Tags: {string.Join(", ", bookmark.Tags)}");
        }

        Console.WriteLine();
        Console.WriteLine("All Tags:");
        await foreach (var tag in tags.GetAllAsync())
        {
            Console.WriteLine($"  - {tag}");
        }

        Console.WriteLine();
        Console.WriteLine("✅ User Preferences example completed!");
        Console.WriteLine("Files created: user_preferences.json, user_bookmarks.json, user_tags.json");
    }

    private static async Task ShoppingCartExampleAsync()
    {
        Console.WriteLine("=== Shopping Cart Example ===");
        Console.WriteLine("An e-commerce shopping cart using database collections.");
        Console.WriteLine();

        var database = await DatabaseFactory.CreateFileDatabaseAsync("ShoppingCart", GetDataPath("shopping_cart.db"));
        
        try
        {
            // Create collections
            var productsCollection = await database.CreateCollectionAsync<ShoppingProduct>("products");
            await productsCollection.CreateIndexAsync("category_index", p => p.Category);
            await productsCollection.CreateIndexAsync("price_index", p => p.Price);

            var cartCollection = await database.CreateCollectionAsync<CartItem>("cart");
            var ordersCollection = await database.CreateCollectionAsync<Order>("orders");

            Console.WriteLine("Setting up shopping cart system...");

            // Add products
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var products = new[]
                {
                    new ShoppingProduct { Id = 1, Name = "Wireless Headphones", Category = "Electronics", Price = 99.99m, Stock = 50 },
                    new ShoppingProduct { Id = 2, Name = "Programming Book", Category = "Books", Price = 49.99m, Stock = 100 },
                    new ShoppingProduct { Id = 3, Name = "Coffee Mug", Category = "Accessories", Price = 12.99m, Stock = 200 },
                    new ShoppingProduct { Id = 4, Name = "Laptop Stand", Category = "Electronics", Price = 79.99m, Stock = 30 },
                    new ShoppingProduct { Id = 5, Name = "Notebook", Category = "Office", Price = 8.99m, Stock = 150 }
                };

                foreach (var product in products)
                {
                    await productsCollection.AddAsync(product);
                    Console.WriteLine($"  Added product: {product.Name} - ${product.Price}");
                }
            });

            // Simulate adding items to cart
            Console.WriteLine();
            Console.WriteLine("Adding items to shopping cart...");
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var cartItems = new[]
                {
                    new CartItem { ProductId = 1, Quantity = 2, AddedAt = DateTime.UtcNow },
                    new CartItem { ProductId = 2, Quantity = 1, AddedAt = DateTime.UtcNow },
                    new CartItem { ProductId = 3, Quantity = 3, AddedAt = DateTime.UtcNow }
                };

                foreach (var item in cartItems)
                {
                    await cartCollection.AddAsync(item);
                    Console.WriteLine($"  Added to cart: Product {item.ProductId} x{item.Quantity}");
                }
            });

            // Simulate order creation
            Console.WriteLine();
            Console.WriteLine("Creating order...");
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var order = new Order
                {
                    Id = Guid.NewGuid().ToString(),
                    CustomerId = "CUST001",
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = 262.95m,
                    Status = "Pending",
                    Items = new[]
                    {
                        new OrderItem { ProductId = 1, Quantity = 2, Price = 99.99m },
                        new OrderItem { ProductId = 2, Quantity = 1, Price = 49.99m },
                        new OrderItem { ProductId = 3, Quantity = 3, Price = 12.99m }
                    }
                };

                Console.WriteLine($"  Order created: {order.Id}");
                await ordersCollection.AddAsync(order);
                Console.WriteLine($"  Total amount: ${order.TotalAmount}");
                Console.WriteLine($"  Status: {order.Status}");
            });

            Console.WriteLine();
            Console.WriteLine("✅ Shopping Cart example completed!");
            Console.WriteLine("Database file: shopping_cart.db");
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task BlogSystemExampleAsync()
    {
        Console.WriteLine("=== Blog System Example ===");
        Console.WriteLine("A blog system with posts, comments, and tags using database collections.");
        Console.WriteLine();

        var database = await DatabaseFactory.CreateFileDatabaseAsync("BlogSystem", GetDataPath("blog_system.db"));
        
        try
        {
            // Create collections
            var postsCollection = await database.CreateCollectionAsync<BlogPost>("posts");
            await postsCollection.CreateIndexAsync("author_index", p => p.Author);
            await postsCollection.CreateIndexAsync("publish_date_index", p => p.PublishDate);

            var commentsCollection = await database.CreateCollectionAsync<Comment>("comments");
            await commentsCollection.CreateIndexAsync("post_id_index", c => c.PostId);

            var tagsCollection = await database.CreateCollectionAsync<Tag>("tags");
            await tagsCollection.CreateIndexAsync("tag_name_index", t => t.Name);

            Console.WriteLine("Setting up blog system...");

            // Add blog posts
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var posts = new[]
                {
                    new BlogPost
                    {
                        Id = 1,
                        Title = "Getting Started with PersistX",
                        Content = "PersistX is a powerful persistent collection library...",
                        Author = "John Doe",
                        PublishDate = DateTime.UtcNow.AddDays(-5),
                        Tags = new[] { "persistx", "csharp", "database" },
                        IsPublished = true
                    },
                    new BlogPost
                    {
                        Id = 2,
                        Title = "Advanced Database Techniques",
                        Content = "In this post, we'll explore advanced database techniques...",
                        Author = "Jane Smith",
                        PublishDate = DateTime.UtcNow.AddDays(-3),
                        Tags = new[] { "database", "performance", "optimization" },
                        IsPublished = true
                    },
                    new BlogPost
                    {
                        Id = 3,
                        Title = "Building Scalable Applications",
                        Content = "Scalability is crucial for modern applications...",
                        Author = "John Doe",
                        PublishDate = DateTime.UtcNow.AddDays(-1),
                        Tags = new[] { "scalability", "architecture", "performance" },
                        IsPublished = false
                    }
                };

                foreach (var post in posts)
                {
                    await postsCollection.AddAsync(post);
                    Console.WriteLine($"  Added post: {post.Title} by {post.Author}");
                }
            });

            // Add comments
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var comments = new[]
                {
                    new Comment { Id = 1, PostId = 1, Author = "Alice", Content = "Great post!", CreatedAt = DateTime.UtcNow.AddDays(-4) },
                    new Comment { Id = 2, PostId = 1, Author = "Bob", Content = "Very helpful, thanks!", CreatedAt = DateTime.UtcNow.AddDays(-3) },
                    new Comment { Id = 3, PostId = 2, Author = "Charlie", Content = "Looking forward to more posts like this.", CreatedAt = DateTime.UtcNow.AddDays(-2) }
                };

                foreach (var comment in comments)
                {
                    await commentsCollection.AddAsync(comment);
                    Console.WriteLine($"  Added comment by {comment.Author} on post {comment.PostId}");
                }
            });

            // Add tags
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var tags = new[]
                {
                    new Tag { Id = 1, Name = "persistx", PostCount = 1 },
                    new Tag { Id = 2, Name = "csharp", PostCount = 1 },
                    new Tag { Id = 3, Name = "database", PostCount = 2 },
                    new Tag { Id = 4, Name = "performance", PostCount = 2 },
                    new Tag { Id = 5, Name = "optimization", PostCount = 1 },
                    new Tag { Id = 6, Name = "scalability", PostCount = 1 },
                    new Tag { Id = 7, Name = "architecture", PostCount = 1 }
                };

                foreach (var tag in tags)
                {
                    await tagsCollection.AddAsync(tag);
                    Console.WriteLine($"  Added tag: {tag.Name} ({tag.PostCount} posts)");
                }
            });

            Console.WriteLine();
            Console.WriteLine("Blog System Summary:");
            Console.WriteLine("  Posts: 3 (2 published, 1 draft)");
            Console.WriteLine("  Comments: 3");
            Console.WriteLine("  Tags: 7");

            Console.WriteLine();
            Console.WriteLine("✅ Blog System example completed!");
            Console.WriteLine("Database file: blog_system.db");
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task InventoryManagementExampleAsync()
    {
        Console.WriteLine("=== Inventory Management Example ===");
        Console.WriteLine("An inventory management system with products, suppliers, and transactions.");
        Console.WriteLine();

        var database = await DatabaseFactory.CreateFileDatabaseAsync("Inventory", GetDataPath("inventory.db"));
        
        try
        {
            // Create collections
            var productsCollection = await database.CreateCollectionAsync<InventoryProduct>("products");
            await productsCollection.CreateIndexAsync("category_index", p => p.Category);
            await productsCollection.CreateIndexAsync("supplier_index", p => p.SupplierId);

            var suppliersCollection = await database.CreateCollectionAsync<Supplier>("suppliers");
            await suppliersCollection.CreateIndexAsync("supplier_name_index", s => s.Name);

            var transactionsCollection = await database.CreateCollectionAsync<InventoryTransaction>("transactions");
            await transactionsCollection.CreateIndexAsync("product_transaction_index", t => t.ProductId);
            await transactionsCollection.CreateIndexAsync("transaction_date_index", t => t.TransactionDate);

            Console.WriteLine("Setting up inventory management system...");

            // Add suppliers
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var suppliers = new[]
                {
                    new Supplier { Id = 1, Name = "TechCorp Supplies", ContactEmail = "orders@techcorp.com", Phone = "555-0101" },
                    new Supplier { Id = 2, Name = "Office Depot", ContactEmail = "business@officedepot.com", Phone = "555-0102" },
                    new Supplier { Id = 3, Name = "Electronics Plus", ContactEmail = "sales@electronicsplus.com", Phone = "555-0103" }
                };

                foreach (var supplier in suppliers)
                {
                    await suppliersCollection.AddAsync(supplier);
                    Console.WriteLine($"  Added supplier: {supplier.Name}");
                }
            });

            // Add products
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var products = new[]
                {
                    new InventoryProduct { Id = 1, Name = "Laptop", Category = "Electronics", SupplierId = 1, CurrentStock = 25, MinStock = 5, MaxStock = 100, UnitPrice = 999.99m },
                    new InventoryProduct { Id = 2, Name = "Office Chair", Category = "Furniture", SupplierId = 2, CurrentStock = 15, MinStock = 3, MaxStock = 50, UnitPrice = 199.99m },
                    new InventoryProduct { Id = 3, Name = "Monitor", Category = "Electronics", SupplierId = 3, CurrentStock = 40, MinStock = 10, MaxStock = 80, UnitPrice = 299.99m },
                    new InventoryProduct { Id = 4, Name = "Desk", Category = "Furniture", SupplierId = 2, CurrentStock = 8, MinStock = 2, MaxStock = 30, UnitPrice = 399.99m },
                    new InventoryProduct { Id = 5, Name = "Keyboard", Category = "Electronics", SupplierId = 1, CurrentStock = 60, MinStock = 15, MaxStock = 120, UnitPrice = 79.99m }
                };

                foreach (var product in products)
                {
                    await productsCollection.AddAsync(product);
                    Console.WriteLine($"  Added product: {product.Name} (Stock: {product.CurrentStock})");
                }
            });

            // Add transactions
            await database.ExecuteInTransactionAsync(async transaction =>
            {
                var transactions = new[]
                {
                    new InventoryTransaction { Id = 1, ProductId = 1, Type = "IN", Quantity = 10, TransactionDate = DateTime.UtcNow.AddDays(-5), Notes = "Initial stock" },
                    new InventoryTransaction { Id = 2, ProductId = 2, Type = "IN", Quantity = 5, TransactionDate = DateTime.UtcNow.AddDays(-4), Notes = "Restock" },
                    new InventoryTransaction { Id = 3, ProductId = 1, Type = "OUT", Quantity = 2, TransactionDate = DateTime.UtcNow.AddDays(-3), Notes = "Sale" },
                    new InventoryTransaction { Id = 4, ProductId = 3, Type = "IN", Quantity = 20, TransactionDate = DateTime.UtcNow.AddDays(-2), Notes = "Bulk order" },
                    new InventoryTransaction { Id = 5, ProductId = 2, Type = "OUT", Quantity = 1, TransactionDate = DateTime.UtcNow.AddDays(-1), Notes = "Sale" }
                };

                foreach (var trans in transactions)
                {
                    await transactionsCollection.AddAsync(trans);
                    Console.WriteLine($"  Added transaction: {trans.Type} {trans.Quantity} units of product {trans.ProductId}");
                }
            });

            Console.WriteLine();
            Console.WriteLine("Inventory Summary:");
            Console.WriteLine("  Products: 5");
            Console.WriteLine("  Suppliers: 3");
            Console.WriteLine("  Transactions: 5");

            Console.WriteLine();
            Console.WriteLine("Low Stock Alert:");
            Console.WriteLine("  Desk: 8 units (Min: 2) - OK");
            Console.WriteLine("  Office Chair: 15 units (Min: 3) - OK");
            Console.WriteLine("  All other products are well stocked");

            Console.WriteLine();
            Console.WriteLine("✅ Inventory Management example completed!");
            Console.WriteLine("Database file: inventory.db");
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task AllExamplesAsync()
    {
        Console.WriteLine("=== All Real-World Examples ===");
        Console.WriteLine("Running all real-world examples in sequence...");
        Console.WriteLine();

        await TodoApplicationExampleAsync();
        Console.WriteLine();
        Console.WriteLine("Press any key to continue to next example...");
        Console.ReadKey();
        Console.Clear();

        await ConfigurationManagerExampleAsync();
        Console.WriteLine();
        Console.WriteLine("Press any key to continue to next example...");
        Console.ReadKey();
        Console.Clear();

        await UserPreferencesExampleAsync();
        Console.WriteLine();
        Console.WriteLine("Press any key to continue to next example...");
        Console.ReadKey();
        Console.Clear();

        await ShoppingCartExampleAsync();
        Console.WriteLine();
        Console.WriteLine("Press any key to continue to next example...");
        Console.ReadKey();
        Console.Clear();

        await BlogSystemExampleAsync();
        Console.WriteLine();
        Console.WriteLine("Press any key to continue to next example...");
        Console.ReadKey();
        Console.Clear();

        await InventoryManagementExampleAsync();

        Console.WriteLine();
        Console.WriteLine("✅ All Real-World Examples completed!");
    }
}

// Data models for the examples
public class TodoItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public bool IsCompleted { get; set; }
}

public class Bookmark
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();
}

public class CartItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; }
}

public class Order
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public OrderItem[] Items { get; set; } = Array.Empty<OrderItem>();
}

public class OrderItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class BlogPost
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime PublishDate { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public bool IsPublished { get; set; }
}

public class Comment
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PostCount { get; set; }
}

public class InventoryProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public int CurrentStock { get; set; }
    public int MinStock { get; set; }
    public int MaxStock { get; set; }
    public decimal UnitPrice { get; set; }
}

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class InventoryTransaction
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Type { get; set; } = string.Empty; // IN or OUT
    public int Quantity { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class ShoppingProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}
