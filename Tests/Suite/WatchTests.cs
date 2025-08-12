using Tests.Framework;

using Engine;

namespace Tests.Suite;

public class WatchTests : BaseTest
{
    public override string Name => "Watch Tests";
    
    public override Task<TestResult[]> RunAsync()
    {
        TestResult[] tests = [
            RunTest($"{Commands.Watch} command starts without compilation errors", TestWatchCommandStartup)
        ];
        return Task.FromResult(tests);
    }
    
    private void TestWatchCommandStartup()
    {        
        var testDir = Directories.GetTestDirectory("watch");
        CleanupDirectory(testDir);
        SetupProject(testDir);
        
        var result = RunCliCommand(
            Commands.Watch, 
            testDir, 
            timeoutMs: 8000,
            waitForSignal: "Watching for changes"
        );
        
        // Verify watch started successfully
        Assert.IsTrue(result.ReceivedReadySignal, "Watch mode did not start - 'Watching for changes' message not received");
        
        // Watch should start without immediate compilation errors
        AssertNoCompilationErrors(result);
        
        // Basic validation that watch mode initiated (process started and produced output)
        Assert.GreaterThan(0, result.Output.Length + result.Error.Length, $"{Commands.Watch} command produced no output");
        
    }
    
    // TODO: Add more watch command tests here
    // - Test watch mode file change detection
    // - Test watch mode hot reload functionality
    // - Test watch mode server startup (WebServer + NodeServer)
    // - Test watch mode graceful shutdown
}