using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace PersistX.Test.Utils;

/// <summary>
/// Helper class for common test functionality.
/// </summary>
public static class TestHelper
{
    /// <summary>
    /// Gets a test data path for the given filename.
    /// </summary>
    /// <param name="fileName">The filename for the test data</param>
    /// <returns>Full path to the test data file</returns>
    public static string GetTestDataPath(string fileName)
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "persistx_data");
        Directory.CreateDirectory(dataDir);
        var fullPath = Path.Combine(dataDir, fileName);
        Console.WriteLine($"📁 Test data will be saved to: {Path.GetFullPath(fullPath)}");
        return fullPath;
    }

    /// <summary>
    /// Creates a logger factory for tests.
    /// </summary>
    /// <param name="logLevel">The log level to use</param>
    /// <returns>Logger factory instance</returns>
    public static ILoggerFactory CreateLoggerFactory(LogLevel logLevel = LogLevel.Information)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(logLevel);
        });
    }

    /// <summary>
    /// Cleans up test data directory.
    /// </summary>
    /// <param name="fileName">The filename to clean up</param>
    public static void CleanupTestData(string fileName)
    {
        try
        {
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "persistx_data");
            var fullPath = Path.Combine(dataDir, fileName);
            
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            else if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Warning: Could not clean up test data: {ex.Message}");
        }
    }

    /// <summary>
    /// Displays a test header.
    /// </summary>
    /// <param name="testName">The name of the test</param>
    public static void DisplayTestHeader(string testName)
    {
        Console.WriteLine($"\n=== {testName} ===");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays a test section header.
    /// </summary>
    /// <param name="sectionName">The name of the section</param>
    public static void DisplaySectionHeader(string sectionName)
    {
        Console.WriteLine($"\n--- {sectionName} ---");
    }

    /// <summary>
    /// Displays test success message.
    /// </summary>
    /// <param name="testName">The name of the test</param>
    public static void DisplayTestSuccess(string testName)
    {
        Console.WriteLine($"\n✅ {testName} completed successfully!");
    }

    /// <summary>
    /// Displays test failure message.
    /// </summary>
    /// <param name="testName">The name of the test</param>
    /// <param name="error">The error message</param>
    public static void DisplayTestFailure(string testName, string error)
    {
        Console.WriteLine($"\n❌ {testName} failed! Error: {error}");
    }

    /// <summary>
    /// Displays test failure with exception details.
    /// </summary>
    /// <param name="testName">The name of the test</param>
    /// <param name="ex">The exception</param>
    public static void DisplayTestFailure(string testName, Exception ex)
    {
        Console.WriteLine($"\n❌ {testName} failed! Error: {ex.Message}");
        Console.WriteLine($"Exception type: {ex.GetType().Name}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }

    /// <summary>
    /// Waits for user input with a message.
    /// </summary>
    /// <param name="message">The message to display</param>
    public static void WaitForUserInput(string message = "Press any key to continue...")
    {
        Console.WriteLine($"\n{message}");
        Console.ReadKey();
    }

    /// <summary>
    /// Formats bytes into a human-readable string.
    /// </summary>
    /// <param name="bytes">The number of bytes</param>
    /// <returns>Formatted string</returns>
    public static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number = number / 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    /// <summary>
    /// Formats a time span into a human-readable string.
    /// </summary>
    /// <param name="timeSpan">The time span</param>
    /// <returns>Formatted string</returns>
    public static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalMilliseconds < 1000)
            return $"{timeSpan.TotalMilliseconds:F2} ms";
        else if (timeSpan.TotalSeconds < 60)
            return $"{timeSpan.TotalSeconds:F2} s";
        else
            return $"{timeSpan.TotalMinutes:F2} m";
    }
}

