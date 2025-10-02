using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Add;

public sealed class AddPageCreatesFiles : ITestCase
{
    public string Name => "add-page creates page skeleton";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);
        // Ensure baseline seed exists with packages so .bin CLI is available
        WorkspaceManager.EnsureSeedWorkspaceReady(context);
        string seedDir = Path.Combine(testDir, Folders.Seed);

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
