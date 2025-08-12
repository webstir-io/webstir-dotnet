using Tests.Framework;
using Engine;

namespace Tests.Suite;

public class DemoTests : BaseTest
{
    public override string Name => "Demo Tests";
    
    public override Task<TestResult[]> RunAsync()
    {
        TestResult[] tests = [
            RunTest($"{Commands.Demo} command creates project without errors", TestDemoCommandSuccess)
        ];
        return Task.FromResult(tests);
    }
    
    private void TestDemoCommandSuccess()
    {
        var testDir = Directories.GetTestDirectory("demo");
        CleanupDirectory(testDir);
        Directory.CreateDirectory(testDir);

        var result = RunCliCommand($"{Commands.Demo} {testDir}", Directory.GetCurrentDirectory(), timeoutMs: 8000);
    
        if (result.TimedOut)
            Assert.Fail($"{Commands.Demo} command timed out");
        
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Demo} command failed. Error: {result.Error}");
        
        // Check if demo directory was created by the command
        Assert.IsTrue(Directory.Exists(testDir), "Demo directory not found");
        
        // Verify essential files exist in correct structure
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "src", "client", "app", "app.css")), "app.css not found");
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "src", "client", "app", "app.ts")), "app.ts not found");
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "src", "client", "app", "app.html")), "app.html not found");
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "package.json")), "package.json not found");
    }
    
    // TODO: Add more demo command tests here
    // - Test demo with different project names
    // - Test demo command validation (invalid names)
    // - Test demo project structure validation
    // - Test demo project can be built after creation
}