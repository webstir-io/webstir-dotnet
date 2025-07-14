using Tests.Framework;
using Engine;
namespace Tests.Suite;

public class BuildTests : BaseTest
{
    public override string Name => "Build Tests";
    
    public override Task<TestResult[]> RunAsync()
    {
        TestResult[] tests = [
            RunTest($"{App.Commands.Build} command runs without compilation errors", TestBuildCommandSuccess)
        ];
        return Task.FromResult(tests);
    }
    
    private void TestBuildCommandSuccess()
    {        
        var testDir = Directories.GetTestDirectory("build");
        CleanupDirectory(testDir);
        SetupProject(testDir);
        
        var result = RunCliCommand(App.Commands.Build, testDir, timeoutMs: 10000);
        
        if (result.TimedOut)
            Assert.Fail($"{App.Commands.Build} command timed out");
        
        Assert.AreEqual(0, result.ExitCode, $"{App.Commands.Build} command failed. Error: {result.Error}");
        
        // Verify build outputs exist
        var buildDir = Path.Combine(testDir, "build");
        Assert.IsTrue(Directory.Exists(buildDir), "Build directory does not exist");
    }
    
    // TODO: Add more build command tests here
    // - Test build with TypeScript errors
    // - Test build with CSS import errors  
    // - Test build output validation
    // - Test build performance benchmarks
}