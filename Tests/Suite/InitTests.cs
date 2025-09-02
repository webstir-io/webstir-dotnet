using Tests.Framework;
using Engine;

namespace Tests.Suite;

public class InitTests : BaseTest
{
    public override string Name => "Init Tests";
    
    public override Task<TestResult[]> RunAsync()
    {
        List<TestResult> tests = [];
        tests.Add(RunTest($"{Commands.Init} command creates default project", TestInitDefault));
        if (Tests.Framework.TestMode.IsFull)
        {
            tests.Add(RunTest($"{Commands.Init} command creates named project", TestInitNamed));
        }
        return Task.FromResult(tests.ToArray());
    }
    
    private void TestInitDefault()
    {
        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);

        // Clean only the seed project to avoid removing other outputs
        string seedDir = Path.Combine(testDir, Folders.Seed);
        CleanupDirectory(seedDir);

        ProcessRunner.ProcessResult result = RunCliCommand(Commands.Init, testDir, timeoutMs: 10000);
        
        if (result.TimedOut)
            Assert.Fail($"{Commands.Init} command timed out");
        
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Init} command failed. Error: {result.Error}");
        
        // Verify essential files were created under the seed project
        seedDir = Path.Combine(testDir, Folders.Seed);
        Assert.IsTrue(File.Exists(Path.Combine(seedDir, Folders.Src, Folders.Client, Folders.App, "app.css")), "app.css not found under seed/src/client/app");
        Assert.IsTrue(File.Exists(Path.Combine(seedDir, Folders.Src, Folders.Client, Folders.App, "app.ts")), "app.ts not found under seed/src/client/app");
        Assert.IsTrue(File.Exists(Path.Combine(seedDir, Folders.Src, Folders.Client, Folders.App, "app.html")), "app.html not found under seed/src/client/app");
        
        // Verify project structure under seed
        Assert.IsTrue(Directory.Exists(Path.Combine(seedDir, Folders.Src)), "seed/src directory not found");
        Assert.IsTrue(File.Exists(Path.Combine(seedDir, Files.PackageJson)), "seed/package.json not found");
    }
    
    private void TestInitNamed()
    {
        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "my-app";
        string namedDir = Path.Combine(testDir, projectName);
        CleanupDirectory(namedDir);
        ProcessRunner.ProcessResult result = RunCliCommand($"{Commands.Init} --project-name {projectName}", testDir, timeoutMs: 10000);

        if (result.TimedOut)
            Assert.Fail($"{Commands.Init} named command timed out");
        
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Init} named command failed. Error: {result.Error}");
        
        // Verify project directory was created with the provided name
        namedDir = Path.Combine(testDir, projectName);
        Assert.IsTrue(Directory.Exists(namedDir), "Named project directory not found");
        
        // Verify essential files were created under the seed directory
        Assert.IsTrue(File.Exists(Path.Combine(namedDir, Folders.Src, Folders.Client, Folders.App, "app.css")), "app.css not found under <name>/src/client/app");
        Assert.IsTrue(File.Exists(Path.Combine(namedDir, Folders.Src, Folders.Client, Folders.App, "app.ts")), "app.ts not found under <name>/src/client/app");
        Assert.IsTrue(File.Exists(Path.Combine(namedDir, Folders.Src, Folders.Client, Folders.App, "app.html")), "app.html not found under <name>/src/client/app");
        Assert.IsTrue(File.Exists(Path.Combine(namedDir, Files.PackageJson)), "<name>/package.json not found");
    }
    
    // TODO: Add more init tests
    // - Test init with existing directory (should fail or warn)
    // - Test init with invalid project names
    // - Test init creates proper project structure for build/watch/publish
}
