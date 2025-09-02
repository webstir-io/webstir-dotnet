using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tests.Framework;
using PublishWorkflowTests = Tests.Workflows.Publish.PublishTests;
using BuildWorkflowTests = Tests.Workflows.Build.BuildTests;
using InitWorkflowTests = Tests.Workflows.Init.InitTests;
using WatchWorkflowTests = Tests.Workflows.Watch.WatchTests;
using HelpWorkflowTests = Tests.Workflows.Help.HelpTests;

namespace Tests;

public class TestOptions
{
    public bool ShowHelp { get; set; }
    public List<string> TestSuites { get; set; } = [];
    public bool? RunFull { get; set; }
}

public class Program
{
    private static async Task Main(string[] args)
    {
        // Parse command line arguments
        TestOptions options = ParseArguments(args);
        
        if (options.ShowHelp)
        {
            ShowHelp();
            return;
        }
        
        // Set test mode (CLI overrides env)
        if (options.RunFull.HasValue)
        {
            TestMode.SetFull(options.RunFull.Value);
        }

        // Setup dependency injection
        ServiceCollection services = new();
        ConfigureServices(services);
        
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        
        // Get services from DI container
        ITestRunner testRunner = serviceProvider.GetRequiredService<ITestRunner>();
        ITestOutputManager outputManager = serviceProvider.GetRequiredService<ITestOutputManager>();
        
        // Run tests
        TestSummary summary = options.TestSuites.Any() 
            ? await testRunner.RunTestsAsync(options.TestSuites)
            : await testRunner.RunAllTestsAsync();
        
        // Output results
        await outputManager.WriteResultsAsync(summary, null, null);
        
        // Exit with error code if tests failed
        Environment.Exit(summary.FailedTests > 0 ? 1 : 0);
    }
    
    private static TestOptions ParseArguments(string[] args)
    {
        TestOptions options = new();
        
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
                case "--full" or "full":
                    options.RunFull = true;
                    break;
                case "--quick" or "quick":
                    options.RunFull = false;
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
        Console.WriteLine("  (none)               Run quick tests (init, build, publish)");
        Console.WriteLine("  --full               Run full test suite (includes watch, help, extras)");
        Console.WriteLine("  --quick              Force quick mode even if env is set to full");
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
        Console.WriteLine("  dotnet run                    # Run quick tests");
        Console.WriteLine("  dotnet run -- --full          # Run full suite");
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
        services.AddTransient<ITestSuite, InitWorkflowTests>();
        services.AddTransient<ITestSuite, BuildWorkflowTests>();
        services.AddTransient<ITestSuite, WatchWorkflowTests>();
        services.AddTransient<ITestSuite, PublishWorkflowTests>();
        services.AddTransient<ITestSuite, HelpWorkflowTests>();
        
        // Register test runner and output manager
        services.AddTransient<ITestRunner, TestRunner>();
        services.AddTransient<ITestOutputManager, TestOutputManager>();
    }
}
