using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Init;

public sealed class InitCreatesDefaultProject : ITestCase
{
    public string Name => "Init command creates default project";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);

        // Cleanup any previous default seed
        string seedDir = Path.Combine(testDir, Folders.Seed);
        if (Directory.Exists(seedDir))
        {
            try
            {
                Directory.Delete(seedDir, recursive: true);
            }
            catch { }
        }

        ProcessRunner.ProcessResult result = context.Cli.Run(Commands.Init, testDir, timeoutMs: 10000);

        Assert.AreEqual(0, result.ExitCode, $"{Commands.Init} command failed. Error: {result.Error}");

        // Verify essential files
        Assert.IsTrue(File.Exists(Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.App, "app.css")), "app.css missing");
        // app.ts removed by design; pages import only what they need
        Assert.IsTrue(File.Exists(Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.App, "app.html")), "app.html missing");
        Assert.IsTrue(File.Exists(Path.Combine(seedDir, Files.PackageJson)), "package.json missing");
    }
}
