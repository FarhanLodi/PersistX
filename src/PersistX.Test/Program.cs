using PersistX.Test.Demos.Advanced;
using PersistX.Test.Demos.Basic;
using PersistX.Test.Tests.AdvancedStorage;
using PersistX.Test.Tests.Performance;
using PersistX.Test.Utils;

namespace PersistX.Test;

/// <summary>
/// Main entry point for the PersistX test console application.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        var menuManager = new TestMenuManager();

        // Basic Demos
        menuManager.AddMenuItem(
            "📝 File-Based Collections Demo",
            "Learn about easy-to-use persistent collections",
            FileBasedCollectionsDemo.RunDemoAsync,
            TestCategory.BasicDemos);

        // Advanced Demos
        menuManager.AddMenuItem(
            "🏢 Database Collections Demo",
            "Learn about enterprise-grade collections with transactions",
            DatabaseCollectionsDemo.RunDemoAsync,
            TestCategory.AdvancedDemos);

        menuManager.AddMenuItem(
            "🌟 Real-World Examples",
            "See PersistX in practical applications",
            RealWorldExamples.RunExamplesAsync,
            TestCategory.AdvancedDemos);

        // Core Tests
        menuManager.AddMenuItem(
            "🚀 Performance Tests",
            "See how fast PersistX collections are",
            async () => await new PerformanceTests().RunTestAsync(),
            TestCategory.CoreTests);

        // Advanced Storage Tests
        menuManager.AddMenuItem(
            "🔐 Encryption Demo",
            "Test encryption with automatic appendable functionality",
            AppendableEncryptionDemo.RunAppendableDemoAsync,
            TestCategory.AdvancedStorage);

        menuManager.AddMenuItem(
            "📝 Write-Ahead Logging Test",
            "Test WAL for crash recovery and data durability",
            WriteAheadLogTest.RunWalTestAsync,
            TestCategory.AdvancedStorage);

        menuManager.AddMenuItem(
            "🗜️ Compression Test",
            "Test GZip and Deflate compression features",
            CompressionTest.RunCompressionTestAsync,
            TestCategory.AdvancedStorage);

        menuManager.AddMenuItem(
            "💾 Backup & Restore Test",
            "Test automated backup and restore functionality",
            BackupRestoreTest.RunBackupRestoreTestAsync,
            TestCategory.AdvancedStorage);

        menuManager.AddMenuItem(
            "📊 Storage Statistics Test",
            "Test storage statistics and monitoring features",
            StorageStatisticsTest.RunStorageStatisticsTestAsync,
            TestCategory.AdvancedStorage);

        menuManager.AddMenuItem(
            "🗺️ Memory-Mapped I/O Test",
            "Test memory-mapped file operations for fast I/O",
            MemoryMappedIOTest.RunMemoryMappedIOTestAsync,
            TestCategory.AdvancedStorage);

        // About
        menuManager.AddMenuItem(
            "📖 About PersistX",
            "Learn about the project and its features",
            ShowAboutPersistX,
            TestCategory.General);

        await menuManager.ShowMenuAsync();
    }

    private static async Task ShowAboutPersistX()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                                                                              ║");
        Console.WriteLine("║                              📖 About PersistX 📖                            ║");
        Console.WriteLine("║                                                                              ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        
        Console.WriteLine("🎯 **What is PersistX?**");
        Console.WriteLine("PersistX is a revolutionary .NET library that bridges the gap between simple");
        Console.WriteLine("file-based collections and full database systems. It provides enterprise-grade");
        Console.WriteLine("persistent collections with the simplicity of traditional collections but the");
        Console.WriteLine("power of embedded databases.");
        Console.WriteLine();
        
        Console.WriteLine("✨ **Key Features:**");
        Console.WriteLine("• 📝 **Simple API** - Use collections like you always have");
        Console.WriteLine("• 🏢 **Enterprise Features** - Transactions, indexing, crash recovery");
        Console.WriteLine("• 🚀 **High Performance** - Optimized for large datasets");
        Console.WriteLine("• 🔒 **Data Safety** - Write-Ahead Logging, encryption, backups");
        Console.WriteLine("• 🔧 **Flexible** - Choose file-based or database collections");
        Console.WriteLine("• ⚡ **Modern** - Full async/await support, .NET 9.0 ready");
        Console.WriteLine();
        
        Console.WriteLine("🎯 **Perfect For:**");
        Console.WriteLine("• Desktop Applications - Settings, user data, local storage");
        Console.WriteLine("• Web Applications - Session storage, caching, temporary data");
        Console.WriteLine("• Data Processing - ETL pipelines, data analysis, reporting");
        Console.WriteLine("• IoT Applications - Device data logging, sensor readings");
        Console.WriteLine("• Gaming - Save games, player progress, leaderboards");
        Console.WriteLine("• Enterprise Software - Configuration management, audit logs");
        Console.WriteLine();
        
        Console.WriteLine("🔧 **Advanced Storage Features (v2.0.0):**");
        Console.WriteLine("• 📝 **Write-Ahead Logging (WAL)** - Crash recovery and durability");
        Console.WriteLine("• 🗺️ **Memory Mapping** - Fast file I/O operations");
        Console.WriteLine("• 🗜️ **Compression** - GZip and Deflate compression");
        Console.WriteLine("• 🔐 **Encryption** - AES-256 encryption at rest");
        Console.WriteLine("• 💾 **Backup & Restore** - Automated backup and recovery");
        Console.WriteLine("• 📊 **Storage Statistics** - Comprehensive monitoring");
        Console.WriteLine();
        
        Console.WriteLine("📦 **Installation:**");
        Console.WriteLine("```bash");
        Console.WriteLine("dotnet add package PersistX");
        Console.WriteLine("```");
        Console.WriteLine();
        
        Console.WriteLine("📄 **License:** MIT License");
        Console.WriteLine("🤝 **Contributing:** We welcome contributions! Please see our contributing guidelines.");
        Console.WriteLine();
        
        await Task.CompletedTask;
    }
}   