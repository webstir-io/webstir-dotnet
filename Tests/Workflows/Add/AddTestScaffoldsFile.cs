using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Add;

public sealed class AddTestScaffoldsFile : ITestCase
{
    public string Name => "add-test creates test file and types";
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

        // Run add-test to create a home page test
        ProcessRunner.ProcessResult result = context.Cli.Run(
            $"{Commands.AddTest} client/pages/home/home {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.AreEqual(0, result.ExitCode, $"{Commands.AddTest} failed. Error: {result.Error}");

        // Verify file exists
        string expectedTest = Path.Combine(seedDir, Folders.Src, Folders.Client, Folders.Pages, Folders.Home, Folders.Tests, "home.test.ts");
        Assert.IsTrue(File.Exists(expectedTest), $"Test file not created at {expectedTest}");

        // Verify ambient types package exists
        string typesIndex = Path.Combine(seedDir, "types", "webstir", "index.d.ts");
        Assert.IsTrue(File.Exists(typesIndex), "Ambient types not present at types/webstir/index.d.ts");
    }
}

