namespace PersistX.Test.Utils;

/// <summary>
/// Manages the test menu and navigation for the PersistX test console application.
/// </summary>
public class TestMenuManager
{
    private readonly List<TestMenuItem> _menuItems = new();
    private int _nextId = 1;

    /// <summary>
    /// Adds a menu item to the test menu.
    /// </summary>
    /// <param name="title">The title of the menu item</param>
    /// <param name="description">The description of the menu item</param>
    /// <param name="action">The action to execute when selected</param>
    /// <param name="category">The category of the menu item</param>
    public void AddMenuItem(string title, string description, Func<Task> action, TestCategory category = TestCategory.General)
    {
        _menuItems.Add(new TestMenuItem
        {
            Id = _nextId++,
            Title = title,
            Description = description,
            Action = action,
            Category = category
        });
    }

    /// <summary>
    /// Displays the main menu and handles user input.
    /// </summary>
    public async Task ShowMenuAsync()
    {
        while (true)
        {
            Console.Clear();
            DisplayHeader();
            DisplayMenu();
            
            Console.Write("\nSelect an option (0 to exit): ");
            var input = Console.ReadLine();
            
            if (string.IsNullOrEmpty(input) || input == "0")
            {
                Console.WriteLine("\n👋 Thank you for exploring PersistX!");
                Console.WriteLine("Happy coding! 🚀");
                break;
            }

            if (int.TryParse(input, out var selection))
            {
                await ExecuteMenuItemAsync(selection);
            }
            else
            {
                Console.WriteLine("❌ Invalid selection. Please enter a number.");
                TestHelper.WaitForUserInput();
            }
        }
    }

    /// <summary>
    /// Displays the application header.
    /// </summary>
    private void DisplayHeader()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                                                                              ║");
        Console.WriteLine("║                            🚀 PersistX Test Console 🚀                       ║");
        Console.WriteLine("║                                                                              ║");
        Console.WriteLine("║                    High-Performance Persistent Collections                   ║");
        Console.WriteLine("║                                                                              ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays the menu items organized by category.
    /// </summary>
    private void DisplayMenu()
    {
        var categories = new Dictionary<TestCategory, List<TestMenuItem>>();
        
        foreach (var item in _menuItems)
        {
            if (!categories.ContainsKey(item.Category))
                categories[item.Category] = new List<TestMenuItem>();
            categories[item.Category].Add(item);
        }

        foreach (var category in categories)
        {
            Console.WriteLine($"\n📚 {GetCategoryTitle(category.Key)}");
            Console.WriteLine(new string('─', 50));
            
            foreach (var item in category.Value)
            {
                Console.WriteLine($"{item.Id,2}) {item.Title}");
                Console.WriteLine($"    {item.Description}");
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Gets the display title for a test category.
    /// </summary>
    /// <param name="category">The test category</param>
    /// <returns>The display title</returns>
    private string GetCategoryTitle(TestCategory category)
    {
        return category switch
        {
            TestCategory.BasicDemos => "Basic Demos",
            TestCategory.AdvancedDemos => "Advanced Demos",
            TestCategory.CoreTests => "Core Functionality Tests",
            TestCategory.AdvancedStorage => "Advanced Storage Tests",
            TestCategory.Performance => "Performance Tests",
            TestCategory.Examples => "Real-World Examples",
            _ => "General"
        };
    }

    /// <summary>
    /// Executes the selected menu item.
    /// </summary>
    /// <param name="selection">The menu item ID</param>
    private async Task ExecuteMenuItemAsync(int selection)
    {
        var menuItem = _menuItems.Find(item => item.Id == selection);
        
        if (menuItem == null)
        {
            Console.WriteLine($"❌ Invalid selection. Please choose a number between 1-{_menuItems.Count}.");
            TestHelper.WaitForUserInput();
            return;
        }

        try
        {
            Console.WriteLine($"\n🚀 Starting: {menuItem.Title}");
            Console.WriteLine();
            
            await menuItem.Action();
            
            Console.WriteLine($"\n✅ {menuItem.Title} completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ {menuItem.Title} failed!");
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
        }
        finally
        {
            TestHelper.WaitForUserInput();
        }
    }
}

/// <summary>
/// Represents a menu item in the test console.
/// </summary>
public class TestMenuItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Func<Task> Action { get; set; } = () => Task.CompletedTask;
    public TestCategory Category { get; set; } = TestCategory.General;
}

/// <summary>
/// Categories for organizing test menu items.
/// </summary>
public enum TestCategory
{
    General,
    BasicDemos,
    AdvancedDemos,
    CoreTests,
    AdvancedStorage,
    Performance,
    Examples
}

