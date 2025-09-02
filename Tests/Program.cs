using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tests.Framework;
using Tests.Suite;

namespace Tests;

public class TestOptions
{
    public bool ShowHelp { get; set; }
    public List<string> TestSuites { get; set; } = new();
}

class Program
{
    static async Task Main(string[] args)
    {
        // Parse command line arguments
        var options = ParseArguments(args);
        
        if (options.ShowHelp)
        {
            ShowHelp();
            return;
        }
        
        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        
        using var serviceProvider = services.BuildServiceProvider();
        
        // Get services from DI container
        var testRunner = serviceProvider.GetRequiredService<ITestRunner>();
        var outputManager = serviceProvider.GetRequiredService<ITestOutputManager>();
        
        // Run tests
        var summary = options.TestSuites.Any() 
            ? await testRunner.RunTestsAsync(options.TestSuites)
            : await testRunner.RunAllTestsAsync();
        
        // Output results
        await outputManager.WriteResultsAsync(summary, null, null);
        
        // Exit with error code if tests failed
        Environment.Exit(summary.FailedTests > 0 ? 1 : 0);
    }
    
    private static TestOptions ParseArguments(string[] args)
    {
        TestOptions options = new TestOptions();
        
        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index].ToLowerInvariant())
            {
                case "help" or "--help" or "-h":
                    options.ShowHelp = true;
                    break;
                case "test":
                    // Take only the next argument as the test suite name
                    if (index + 1 < args.Length)
                    {
                        options.TestSuites.Add(args[++index]);
                    }
                    break;
                default:
                    // Ignore unknown arguments
                    break;
            }
        }
        
        return options;
    }
    
    private static void ShowHelp()
    {
        Console.WriteLine("WebStir Test Runner");
        Console.WriteLine();
        Console.WriteLine("Usage: dotnet run [command]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  (none)               Run all tests (default)");
        Console.WriteLine("  test <suite>         Run specific test suite");
        Console.WriteLine("  help                 Show this help message");
        Console.WriteLine();
        Console.WriteLine("Available Test Suites:");
        Console.WriteLine("  init                 - Tests the init command");
        Console.WriteLine("  build                - Tests the build command");
        Console.WriteLine("  watch                - Tests the watch command"); 
        Console.WriteLine("  publish              - Tests the publish command");
        Console.WriteLine("  demo                 - Tests the demo command");
        Console.WriteLine("  help                 - Tests the help command");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run                    # Run all tests");
        Console.WriteLine("  dotnet run help               # Show this help");
        Console.WriteLine("  dotnet run test init          # Run only init tests");
        Console.WriteLine("  dotnet run test build         # Run only build tests");
        Console.WriteLine("  dotnet run test watch         # Run only watch tests");
    }
    
    private static void ConfigureServices(IServiceCollection services)
    {
        // Add logging (errors only for clean output)
        services.AddLogging(builder =>
        {
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Error);
        });
        
        // Register test suites
        services.AddTransient<ITestSuite, InitTests>();
        services.AddTransient<ITestSuite, BuildTests>();
        services.AddTransient<ITestSuite, WatchTests>();
        services.AddTransient<ITestSuite, PublishTests>();
        services.AddTransient<ITestSuite, DemoTests>();
        services.AddTransient<ITestSuite, HelpTests>();
        
        // Register test runner and output manager
        services.AddTransient<ITestRunner, TestRunner>();
        services.AddTransient<ITestOutputManager, TestOutputManager>();
    }
}
