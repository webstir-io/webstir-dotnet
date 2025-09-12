using System;
using System.IO;
using Engine;
using Tests.Framework;

namespace Tests.Pipelines.Core;

public sealed class RobotsTxtExists : ITestCase
{
    public string Name => "robots.txt exists and allows all";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        string seedDir = Path.Combine(testDir, Folders.Seed);

        if (!Directory.Exists(Path.Combine(seedDir, Folders.Src)))
        {
            ProcessRunner.ProcessResult init = context.Cli.Run($"{Commands.Init} {ProjectOptions.ProjectName} {Folders.Seed}", testDir, timeoutMs: 15000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} failed: {init.Error}");
        }

        ProcessRunner.ProcessResult publish = context.Cli.Run($"{Commands.Publish} {ProjectOptions.ProjectName} {Folders.Seed}", testDir, timeoutMs: 20000);
        Assert.AreEqual(0, publish.ExitCode, $"{Commands.Publish} failed: {publish.Error}");

        string robotsPath = Path.Combine(seedDir, Folders.Dist, Folders.Frontend, Files.RobotsTxt);
        Assert.IsTrue(File.Exists(robotsPath), "robots.txt missing in dist/frontend");
        string text = File.ReadAllText(robotsPath);
        Assert.Contains("User-agent: *", text, "Expected allow-all robots.txt");
    }
}

