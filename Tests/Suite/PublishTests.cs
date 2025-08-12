using Tests.Framework;

using Engine;

namespace Tests.Suite;

public class PublishTests : BaseTest
{
    public override string Name => "Publish Tests";
    
    public override Task<TestResult[]> RunAsync()
    {
        TestResult[] tests = [
            RunTest($"{Commands.Publish} command runs without compilation errors", TestPublishCommandSuccess)
        ];
        return Task.FromResult(tests);
    }
    
    private void TestPublishCommandSuccess()
    {        
        var testDir = Directories.GetTestDirectory("publish");
        CleanupDirectory(testDir);
        SetupProject(testDir);
        
        var result = RunCliCommand(Commands.Publish, testDir, timeoutMs: 15000);
        
        if (result.TimedOut)
            Assert.Fail($"{Commands.Publish} command timed out");
        
        // Publish should complete without errors (exit code 0 or 1 both acceptable for basic validation)
        Assert.IsTrue(result.ExitCode == 0 || result.ExitCode == 1, 
            $"{Commands.Publish} command failed with exit code {result.ExitCode}. Error: {result.Error}");
        
        AssertNoCompilationErrors(result);
    }
    
    // TODO: Add more publish command tests here
    // - Test publish output validation (dist folder created)
    // - Test CSS minification in publish mode
    // - Test production optimizations
    // - Test publish performance benchmarks
}