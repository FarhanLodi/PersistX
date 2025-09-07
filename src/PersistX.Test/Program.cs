using PersistX.Test.Demos;
using PersistX.Test.Tests;
using PersistX.Test.Examples;

namespace PersistX.Test;

/// <summary>
/// PersistX Educational Test Application
/// This application helps users learn and understand PersistX features through interactive demos and examples.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 PersistX Educational Test Application");
        Console.WriteLine("========================================");
        Console.WriteLine();
        Console.WriteLine("Welcome to PersistX! This application will help you learn about");
        Console.WriteLine("persistent collections through interactive demos and real-world examples.");
        Console.WriteLine();
        Console.WriteLine("PersistX provides two types of collections:");
        Console.WriteLine("• File-Based Collections: Easy-to-use standalone collections (List, Dictionary, Set)");
        Console.WriteLine("• Database Collections: Enterprise features with transactions, indexing, and multiple backends");
        Console.WriteLine();

        await MainMenuAsync();
    }

    private static async Task MainMenuAsync()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("📚 === PersistX Learning Menu === 📚");
            Console.WriteLine();
            Console.WriteLine("🎯 Choose what you'd like to learn:");
            Console.WriteLine();
            Console.WriteLine("1) 📝 File-Based Collections Demo");
            Console.WriteLine("   Learn about easy-to-use persistent collections");
            Console.WriteLine("   • PersistentList - Store lists of items");
            Console.WriteLine("   • PersistentDictionary - Store key-value pairs");
            Console.WriteLine("   • PersistentSet - Store unique items");
            Console.WriteLine();
            Console.WriteLine("2) 🏢 Database Collections Demo");
            Console.WriteLine("   Learn about enterprise-grade collections");
            Console.WriteLine("   • Transactions and ACID properties");
            Console.WriteLine("   • Indexing for fast searches");
            Console.WriteLine("   • Multiple storage backends");
            Console.WriteLine();
            Console.WriteLine("3) 🚀 Performance Tests");
            Console.WriteLine("   See how fast PersistX collections are");
            Console.WriteLine("   • Speed benchmarks");
            Console.WriteLine("   • Memory usage analysis");
            Console.WriteLine("   • Bulk operations testing");
            Console.WriteLine();
            Console.WriteLine("4) 🌟 Real-World Examples");
            Console.WriteLine("   See PersistX in practical applications");
            Console.WriteLine("   • Todo applications");
            Console.WriteLine("   • Configuration management");
            Console.WriteLine("   • E-commerce systems");
            Console.WriteLine("   • Blog systems");
            Console.WriteLine();
            Console.WriteLine("5) 📖 About PersistX");
            Console.WriteLine("   Learn about the project and its features");
            Console.WriteLine();
            Console.WriteLine("0) 🚪 Exit");
            Console.WriteLine();
            Console.Write("Select an option (0-5): ");
            var input = Console.ReadLine();

            Console.WriteLine();
            switch (input)
            {
                case "1":
                    await FileBasedCollectionsDemo.RunDemoAsync();
                    break;
                case "2":
                    await DatabaseCollectionsDemo.RunDemoAsync();
                    break;
                case "3":
                    await PerformanceTests.RunPerformanceTestsAsync();
                    break;
                case "4":
                    await RealWorldExamples.RunExamplesAsync();
                    break;
                case "5":
                    ShowAboutPersistX();
                    break;
                case "0":
                    Console.WriteLine("👋 Thank you for exploring PersistX!");
                    Console.WriteLine("Happy coding! 🚀");
                    return;
                default:
                    Console.WriteLine("❌ Invalid selection. Please choose a number between 0-5.");
                    break;
            }
        }
    }

    private static void ShowAboutPersistX()
    {
        Console.Clear();
        Console.WriteLine("📖 === About PersistX === 📖");
        Console.WriteLine();
        Console.WriteLine("PersistX is a high-performance persistent collection library for .NET");
        Console.WriteLine("that provides both simple and enterprise-grade data persistence solutions.");
        Console.WriteLine();
        Console.WriteLine("🎯 Key Features:");
        Console.WriteLine("• File-Based Collections: Easy-to-use standalone collections");
        Console.WriteLine("• Database Collections: Enterprise features with transactions and indexing");
        Console.WriteLine("• Multiple Storage Backends: File, In-Memory, and SQLite");
        Console.WriteLine("• ACID Transactions: Reliable data operations");
        Console.WriteLine("• Async APIs: Full async/await support");
        Console.WriteLine("• JSON Serialization: Human-readable data storage");
        Console.WriteLine();
        Console.WriteLine("🏗️ Project Structure:");
        Console.WriteLine("• FileBased/ - Standalone collections (List, Dictionary, Set)");
        Console.WriteLine("• Database/ - Database operations and management");
        Console.WriteLine("• Collections/ - Database-integrated collections");
        Console.WriteLine("• Indexes/ - Indexing system for fast searches");
        Console.WriteLine("• Storage/ - Storage backends (File, Memory, SQLite)");
        Console.WriteLine("• Serialization/ - JSON serialization support");
        Console.WriteLine("• Interfaces/ - All public interfaces");
        Console.WriteLine();
        Console.WriteLine("🚀 Use Cases:");
        Console.WriteLine("• Application settings and configuration");
        Console.WriteLine("• User preferences and bookmarks");
        Console.WriteLine("• Task management and todo lists");
        Console.WriteLine("• E-commerce shopping carts");
        Console.WriteLine("• Blog systems and content management");
        Console.WriteLine("• Inventory management");
        Console.WriteLine("• Logging and metrics collection");
        Console.WriteLine();
        Console.WriteLine("📚 Learning Path:");
        Console.WriteLine("1. Start with File-Based Collections to understand basic concepts");
        Console.WriteLine("2. Explore Database Collections for advanced features");
        Console.WriteLine("3. Run Performance Tests to see the speed");
        Console.WriteLine("4. Check Real-World Examples for practical applications");
        Console.WriteLine();
        Console.WriteLine("🔗 Resources:");
        Console.WriteLine("• GitHub: https://github.com/your-org/persistx");
        Console.WriteLine("• Documentation: Check the README.md file");
        Console.WriteLine("• Examples: Run the demos in this application");
        Console.WriteLine();
        Console.WriteLine("Press any key to return to the main menu...");
        Console.ReadKey();
        Console.Clear();
    }
}
