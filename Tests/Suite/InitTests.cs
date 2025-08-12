using Tests.Framework;
using Engine;

namespace Tests.Suite;

public class InitTests : BaseTest
{
    public override string Name => "Init Tests";
    
    public override Task<TestResult[]> RunAsync()
    {
        var testDir = Directories.GetTestDirectory("init");
        CleanupDirectory(testDir);        

        TestResult[] tests = [
            RunTest($"{Commands.Init} command creates default project", TestInitDefault),
            RunTest($"{Commands.Init} command creates named project", TestInitNamed)
        ];
        return Task.FromResult(tests);
    }
    
    private void TestInitDefault()
    {
        var testDir = Directories.GetTestDirectory(Path.Combine("init", "default"));
        Directory.CreateDirectory(testDir);
        
        var result = RunCliCommand(Commands.Init, testDir, timeoutMs: 10000);
        
        if (result.TimedOut)
            Assert.Fail($"{Commands.Init} command timed out");
        
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Init} command failed. Error: {result.Error}");
        
        // Verify essential files were created in correct structure
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "src", "client", "app", "app.css")), "app.css not found");
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "src", "client", "app", "app.ts")), "app.ts not found");
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "src", "client", "app", "app.html")), "app.html not found");
        
        // Verify project structure
        Assert.IsTrue(Directory.Exists(Path.Combine(testDir, "src")), "src directory not found");
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "package.json")), "package.json not found");
    }
    
    private void TestInitNamed()
    {
        var testDir = Directories.GetTestDirectory(Path.Combine("init", "named"));
        Directory.CreateDirectory(testDir);

        var result = RunCliCommand($"{Commands.Init}", testDir, timeoutMs: 10000);

        if (result.TimedOut)
            Assert.Fail($"{Commands.Init} named command timed out");
        
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Init} named command failed. Error: {result.Error}");
        
        // Verify project directory was created
        Assert.IsTrue(Directory.Exists(testDir), "Named project directory not found");
        
        // Verify essential files were created in the named directory
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "src", "client", "app", "app.css")), "app.css not found");
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "src", "client", "app", "app.ts")), "app.ts not found");
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "src", "client", "app", "app.html")), "app.html not found");
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "package.json")), "package.json not found");
    }
    
    // TODO: Add more init tests
    // - Test init with existing directory (should fail or warn)
    // - Test init with invalid project names
    // - Test init creates proper project structure for build/watch/publish
}