using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Init;

public sealed class InitCreatesNamedProject : ITestCase
{
    public string Name => "Init command creates named project";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);
        string projectName = "seed-named";
        string namedDir = Path.Combine(testDir, projectName);

        if (Directory.Exists(namedDir))
        {
            try
            {
                Directory.Delete(namedDir, recursive: true);
            }
            catch { }
        }

        ProcessRunner.ProcessResult result = context.Cli.Run($"{Commands.Init} --project-name {projectName}", testDir, timeoutMs: 10000);
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Init} command failed. Error: {result.Error}");

        Assert.IsTrue(Directory.Exists(namedDir), "Named project directory not found");
        Assert.IsTrue(File.Exists(Path.Combine(namedDir, Folders.Src, Folders.Client, Folders.App, "app.css")), "app.css missing");
        Assert.IsTrue(File.Exists(Path.Combine(namedDir, Folders.Src, Folders.Client, Folders.App, "app.ts")), "app.ts missing");
        Assert.IsTrue(File.Exists(Path.Combine(namedDir, Folders.Src, Folders.Client, Folders.App, "app.html")), "app.html missing");
        Assert.IsTrue(File.Exists(Path.Combine(namedDir, Files.PackageJson)), "package.json missing");
    }
}

