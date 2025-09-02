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
        string testDir = Directories.OutDirectory.FullName;
        Directory.CreateDirectory(testDir);
        string seedDir = Path.Combine(testDir, Folders.Seed);
        if (!Directory.Exists(Path.Combine(seedDir, Folders.Src)))
        {
            // Initialize seed project if missing
            ProcessRunner.ProcessResult init = RunCliCommand(Commands.Init, testDir, timeoutMs: 10000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");
        }
        
        // Per-suite cleanup: start watch with a clean build folder to ensure fresh compile
        string seedBuild = Path.Combine(seedDir, Folders.Build);
        if (Directory.Exists(seedBuild))
        {
            try { Directory.Delete(seedBuild, recursive: true); } catch { }
        }
        
        ProcessRunner.ProcessResult result = RunCliCommand(
            $"{Commands.Watch} {ProjectOptions.ProjectName} seed",
            testDir,
            timeoutMs: 12000,
            waitForSignal: "Dev Service is running"
        );
        
        // Verify watch started successfully
        Assert.IsTrue(result.ReceivedReadySignal, "Watch mode did not start - readiness message not received");
        
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
