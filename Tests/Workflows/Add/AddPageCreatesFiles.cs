using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Add;

public sealed class AddPageCreatesFiles : ITestCase
{
    public string Name => "add-page creates page skeleton";
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

        // Run add-page
        ProcessRunner.ProcessResult result = context.Cli.Run(
            $"{Commands.AddPage} about {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.AreEqual(0, result.ExitCode, $"{Commands.AddPage} failed. Error: {result.Error}");

        // Verify files
        string pageDir = Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.Pages, "about");
        Assert.IsTrue(File.Exists(Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Html}")), "index.html not created");
        Assert.IsTrue(File.Exists(Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Css}")), "index.css not created");
        Assert.IsTrue(File.Exists(Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Ts}")), "index.ts not created");
    }
}
