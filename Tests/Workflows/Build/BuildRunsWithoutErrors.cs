using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Build;

public sealed class BuildRunsWithoutErrors : ITestCase
{
    public string Name => "Build command runs without compilation errors";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);
        string seedDir = Path.Combine(testDir, Folders.Seed);
        if (!Directory.Exists(Path.Combine(seedDir, Folders.Src)))
        {
            ProcessRunner.ProcessResult init = context.Cli.Run(Commands.Init, testDir, timeoutMs: 10000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");
        }

        // Clean previous build
        string seedBuild = Path.Combine(seedDir, Folders.Build);
        if (Directory.Exists(seedBuild))
        {
            try
            {
                Directory.Delete(seedBuild, recursive: true);
            }
            catch { }
        }

        ProcessRunner.ProcessResult result = context.Cli.Run($"{Commands.Build} {ProjectOptions.ProjectName} seed", testDir, timeoutMs: 10000);

        if (result.TimedOut)
        {
            Assert.Fail($"{Commands.Build} command timed out");
        }

        Assert.AreEqual(0, result.ExitCode, $"{Commands.Build} command failed. Error: {result.Error}");
        context.AssertNoCompilationErrors(result);

        // Verify minimal expected artifacts
        string buildDir = Path.Combine(testDir, Folders.Seed, Folders.Build);
        Assert.IsTrue(Directory.Exists(buildDir), "seed/build directory does not exist");

        string clientPageDir = Path.Combine(buildDir, Folders.Frontend, Folders.Pages, Folders.Home);
        Assert.IsTrue(File.Exists(Path.Combine(clientPageDir, $"{Files.Index}{FileExtensions.Html}")), "client page index.html missing in build");
        Assert.IsTrue(File.Exists(Path.Combine(clientPageDir, $"{Files.Index}{FileExtensions.Js}")), "client page index.js missing in build");
    }
}
