using PersistX.FileBased;

namespace PersistX.Test.Demos.Basic;

/// <summary>
/// Demonstrates file-based persistent collections (List, Dictionary, Set)
/// These are standalone collections that save to JSON files
/// </summary>
public class FileBasedCollectionsDemo
{
    private static string GetDataPath(string fileName)
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "persistx_data");
        Directory.CreateDirectory(dataDir);
        var fullPath = Path.Combine(dataDir, fileName);
        Console.WriteLine($"📁 Data will be saved to: {Path.GetFullPath(fullPath)}");
        return fullPath;
    }
    public static async Task RunDemoAsync()
    {
        Console.WriteLine("=== File-Based Collections Demo ===");
        Console.WriteLine("These collections are easy to use and save data to JSON files.");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("Choose a file-based collection to test:");
            Console.WriteLine("1) PersistentList - Store a list of items");
            Console.WriteLine("2) PersistentDictionary - Store key-value pairs");
            Console.WriteLine("3) PersistentSet - Store unique items");
            Console.WriteLine("4) All Collections Demo - Run all demos");
            Console.WriteLine("0) Back to main menu");
            Console.Write("Select an option: ");

            var input = Console.ReadLine();
            Console.WriteLine();

            switch (input)
            {
                case "1":
                    await TaskListDemoAsync();
                    break;
                case "2":
                    await SettingsDemoAsync();
                    break;
                case "3":
                    await TagsDemoAsync();
                    break;
                case "4":
                    await AllCollectionsDemoAsync();
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

    private static async Task TaskListDemoAsync()
    {
        Console.WriteLine("=== PersistentList Demo ===");
        Console.WriteLine("A PersistentList stores a collection of items and saves them to a JSON file.");
        Console.WriteLine();

        // Create a task list
        var tasks = new PersistentList<string>(GetDataPath("tasks.json"));
        
        Console.WriteLine("Adding tasks...");
        await tasks.AddAsync("Complete project proposal");
        await tasks.AddAsync("Review code changes");
        await tasks.AddAsync("Update documentation");
        await tasks.AddAsync("Test the application");

        Console.WriteLine($"Total tasks: {await tasks.CountAsync()}");
        Console.WriteLine();

        Console.WriteLine("All tasks:");
        await foreach (var task in tasks.GetAllAsync())
        {
            Console.WriteLine($"- {task}");
        }

        Console.WriteLine();
        Console.WriteLine("Checking if 'Complete project proposal' exists:");
        if (await tasks.ContainsAsync("Complete project proposal"))
        {
            Console.WriteLine("✅ Task found!");
        }

        Console.WriteLine();
        Console.WriteLine("Removing 'Review code changes'...");
        await tasks.RemoveAsync("Review code changes");

        Console.WriteLine("Updated task list:");
        await foreach (var task in tasks.GetAllAsync())
        {
            Console.WriteLine($"- {task}");
        }

        Console.WriteLine();
        Console.WriteLine("✅ PersistentList demo completed!");
        Console.WriteLine("Check the 'tasks.json' file in your project directory to see the saved data.");
    }

    private static async Task SettingsDemoAsync()
    {
        Console.WriteLine("=== PersistentDictionary Demo ===");
        Console.WriteLine("A PersistentDictionary stores key-value pairs and saves them to a JSON file.");
        Console.WriteLine();

        // Create a settings dictionary
        var settings = new PersistentDictionary<string, object>(GetDataPath("app_settings.json"));

        Console.WriteLine("Setting application preferences...");
        await settings.SetAsync("theme", "dark");
        await settings.SetAsync("language", "en");
        await settings.SetAsync("notifications_enabled", true);
        await settings.SetAsync("max_file_size", 10485760); // 10MB
        await settings.SetAsync("auto_save", true);
        await settings.SetAsync("user_name", "John Doe");

        Console.WriteLine($"Total settings: {await settings.CountAsync()}");
        Console.WriteLine();

        Console.WriteLine("All settings:");
        await foreach (var kvp in settings.GetAllAsync())
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        Console.WriteLine();
        Console.WriteLine("Getting specific settings:");
        var theme = await settings.GetAsync("theme");
        var maxSize = await settings.GetAsync("max_file_size");
        var notifications = await settings.GetAsync("notifications_enabled");

        Console.WriteLine($"  Theme: {theme}");
        Console.WriteLine($"  Max file size: {maxSize} bytes");
        Console.WriteLine($"  Notifications enabled: {notifications}");

        Console.WriteLine();
        Console.WriteLine("Updating theme to 'light'...");
        await settings.SetAsync("theme", "light");

        Console.WriteLine("Updated theme:");
        Console.WriteLine($"  Theme: {await settings.GetAsync("theme")}");

        Console.WriteLine();
        Console.WriteLine("✅ PersistentDictionary demo completed!");
        Console.WriteLine("Check the 'app_settings.json' file in your project directory to see the saved data.");
    }

    private static async Task TagsDemoAsync()
    {
        Console.WriteLine("=== PersistentSet Demo ===");
        Console.WriteLine("A PersistentSet stores unique items and saves them to a JSON file.");
        Console.WriteLine("Files will be saved as: all_tags.json and important_tags.json");
        Console.WriteLine();

        // Create tag sets
        var allTags = new PersistentSet<string>(GetDataPath("all_tags.json"));
        var importantTags = new PersistentSet<string>(GetDataPath("important_tags.json"));

        Console.WriteLine("Adding tags to main collection...");
        await allTags.AddAsync("work");
        await allTags.AddAsync("personal");
        await allTags.AddAsync("urgent");
        await allTags.AddAsync("important");
        await allTags.AddAsync("project");
        await allTags.AddAsync("meeting");

        Console.WriteLine("Adding important tags...");
        await importantTags.AddAsync("urgent");
        await importantTags.AddAsync("important");
        await importantTags.AddAsync("project");

        Console.WriteLine($"Total unique tags: {await allTags.CountAsync()}");
        Console.WriteLine($"Important tags: {await importantTags.CountAsync()}");
        Console.WriteLine();

        Console.WriteLine("All tags:");
        await foreach (var tag in allTags.GetAllAsync())
        {
            Console.WriteLine($"- {tag}");
        }

        Console.WriteLine();
        Console.WriteLine("Important tags:");
        await foreach (var tag in importantTags.GetAllAsync())
        {
            Console.WriteLine($"- {tag}");
        }

        Console.WriteLine();
        Console.WriteLine("Checking relationships:");
        if (await importantTags.IsSubsetOfAsync(allTags))
        {
            Console.WriteLine("✅ All important tags are in the main tag set");
        }

        Console.WriteLine();
        Console.WriteLine("Checking if 'urgent' exists in main tags:");
        if (await allTags.ContainsAsync("urgent"))
        {
            Console.WriteLine("✅ 'urgent' tag found in main collection");
        }

        Console.WriteLine();
        Console.WriteLine("Removing 'personal' tag...");
        await allTags.RemoveAsync("personal");

        Console.WriteLine("Updated tag list:");
        await foreach (var tag in allTags.GetAllAsync())
        {
            Console.WriteLine($"- {tag}");
        }

        Console.WriteLine();
        Console.WriteLine("✅ PersistentSet demo completed!");
        Console.WriteLine("Check the 'all_tags.json' and 'important_tags.json' files in your project directory.");
    }

    private static async Task AllCollectionsDemoAsync()
    {
        Console.WriteLine("=== All Simple Collections Demo ===");
        Console.WriteLine("This demo shows how all three simple collections work together.");
        Console.WriteLine();

        // Create all collections
        var tasks = new PersistentList<string>(GetDataPath("demo_tasks.json"));
        var settings = new PersistentDictionary<string, object>(GetDataPath("demo_settings.json"));
        var tags = new PersistentSet<string>(GetDataPath("demo_tags.json"));

        Console.WriteLine("Setting up a complete application scenario...");
        Console.WriteLine();

        // Add tasks
        Console.WriteLine("1. Adding tasks:");
        await tasks.AddAsync("Design user interface");
        await tasks.AddAsync("Implement authentication");
        await tasks.AddAsync("Write unit tests");
        await tasks.AddAsync("Deploy to production");

        // Add settings
        Console.WriteLine("2. Setting application preferences:");
        await settings.SetAsync("app_name", "My Application");
        await settings.SetAsync("version", "1.0.0");
        await settings.SetAsync("debug_mode", false);
        await settings.SetAsync("max_users", 1000);

        // Add tags
        Console.WriteLine("3. Adding project tags:");
        await tags.AddAsync("frontend");
        await tags.AddAsync("backend");
        await tags.AddAsync("database");
        await tags.AddAsync("testing");
        await tags.AddAsync("deployment");

        Console.WriteLine();
        Console.WriteLine("=== Application Summary ===");
        Console.WriteLine($"Tasks: {await tasks.CountAsync()}");
        Console.WriteLine($"Settings: {await settings.CountAsync()}");
        Console.WriteLine($"Tags: {await tags.CountAsync()}");
        Console.WriteLine();

        Console.WriteLine("Tasks:");
        await foreach (var task in tasks.GetAllAsync())
        {
            Console.WriteLine($"  - {task}");
        }

        Console.WriteLine();
        Console.WriteLine("Settings:");
        await foreach (var kvp in settings.GetAllAsync())
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        Console.WriteLine();
        Console.WriteLine("Tags:");
        await foreach (var tag in tags.GetAllAsync())
        {
            Console.WriteLine($"  - {tag}");
        }

        Console.WriteLine();
        Console.WriteLine("✅ All Simple Collections demo completed!");
        Console.WriteLine("Check the demo_*.json files in your project directory to see all saved data.");
    }
}
