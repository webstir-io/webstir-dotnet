using Tests.Framework;
using Engine;
namespace Tests.Suite;

public class BuildTests : BaseTest
{
    public override string Name => "Build Tests";
    
    public override Task<TestResult[]> RunAsync()
    {
        List<TestResult> tests = [];
        tests.Add(RunTest($"{Commands.Build} command runs without compilation errors", TestBuildCommandSuccess));
        if (Tests.Framework.TestMode.IsFull)
        {
            tests.Add(RunTest("Build surfaces error when app.html is missing", TestBuildMissingAppHtmlShowsError));
        }
        return Task.FromResult(tests.ToArray());
    }
    
    private void TestBuildCommandSuccess()
    {        
        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);
        string seedDir = Path.Combine(testDir, Folders.Seed);
        if (!Directory.Exists(Path.Combine(seedDir, Folders.Src)))
        {
            // Initialize seed project if missing
            ProcessRunner.ProcessResult init = RunCliCommand(Commands.Init, testDir, timeoutMs: 10000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");
        }
        
        // Per-suite cleanup: clear previous build outputs
        string seedBuild = Path.Combine(seedDir, Folders.Build);
        if (Directory.Exists(seedBuild))
        {
            try { Directory.Delete(seedBuild, recursive: true); } catch { }
        }
        
        ProcessRunner.ProcessResult result = RunCliCommand($"{Commands.Build} {ProjectOptions.ProjectName} seed", testDir, timeoutMs: 10000);
        
        if (result.TimedOut)
            Assert.Fail($"{Commands.Build} command timed out");
        
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Build} command failed. Error: {result.Error}");
        
        // Verify build outputs exist under the seed project
        string buildDir = Path.Combine(testDir, Folders.Seed, Folders.Build);
        Assert.IsTrue(Directory.Exists(buildDir), "seed/build directory does not exist");

        // Client build artifacts
        string clientPageDir = Path.Combine(buildDir, Folders.Client, Folders.Pages, Folders.Home);
        Assert.IsTrue(File.Exists(Path.Combine(clientPageDir, $"{Files.Index}{FileExtensions.Html}")), "client page index.html missing in build");
        Assert.IsTrue(File.Exists(Path.Combine(clientPageDir, $"{Files.Index}{FileExtensions.Js}")), "client page index.js missing in build");
        bool hasCss = File.Exists(Path.Combine(clientPageDir, "index.module.css"))
            || File.Exists(Path.Combine(clientPageDir, $"{Files.Index}{FileExtensions.Css}"));
        Assert.IsTrue(hasCss, "client page CSS missing in build (index.module.css or index.css)");

        // Refresh script should exist in build/client (dev only)
        Assert.IsTrue(
            File.Exists(Path.Combine(buildDir, Folders.Client, Files.RefreshJs)),
            "refresh.js missing in build/client"
        );

        // Server build artifact
        string serverIndexJs = Path.Combine(buildDir, Folders.Server, $"{Files.Index}{FileExtensions.Js}");
        Assert.IsTrue(File.Exists(serverIndexJs), "server index.js missing in build");
    }
    
    // TODO: Add more build command tests here
    // - Test build with TypeScript errors
    // - Test build with CSS import errors  
    // - Test build output validation
    // - Test build performance benchmarks

    private void TestBuildMissingAppHtmlShowsError()
    {
        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);
        string projectName = "seed-missing-app";
        string projectDir = Path.Combine(testDir, projectName);
        if (!Directory.Exists(Path.Combine(projectDir, Folders.Src)))
        {
            ProcessRunner.ProcessResult init = RunCliCommand($"{Commands.Init} --project-name {projectName}", testDir, timeoutMs: 10000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");
        }

        string appHtml = Path.Combine(projectDir, Folders.Src, Folders.Client, Folders.App, "app.html");
        if (File.Exists(appHtml))
        {
            try { File.Delete(appHtml); } catch { }
        }

        ProcessRunner.ProcessResult result = RunCliCommand($"{Commands.Build} {ProjectOptions.ProjectName} {projectName}", testDir, timeoutMs: 10000);

        Assert.AreEqual(0, result.ExitCode, $"{Commands.Build} command failed. Error: {result.Error}");
        string combined = (result.Output ?? string.Empty) + "\n" + (result.Error ?? string.Empty);
        Assert.IsTrue(
            combined.Contains("Base application HTML file not found", StringComparison.OrdinalIgnoreCase),
            "Build output should contain an error about missing app.html"
        );
    }
}
