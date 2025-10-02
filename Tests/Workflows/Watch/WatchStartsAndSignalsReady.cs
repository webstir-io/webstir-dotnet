using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Watch;

public sealed class WatchStartsAndSignalsReady : ITestCase
{
    public string Name => "Watch command starts and signals ready";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);
        string projectName = "seed-watch";
        string seedDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        string seedBuild = Path.Combine(seedDir, Folders.Build);
        if (Directory.Exists(seedBuild))
        {
            try
            {
                Directory.Delete(seedBuild, recursive: true);
            }
            catch { }
        }

        ProcessRunner.ProcessResult result = context.Cli.Run(
            $"{Commands.Watch} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 12000,
            waitForSignal: App.DevService
        );

        Assert.IsTrue(result.ReceivedReadySignal, "Watch mode did not start - readiness message not received");
        context.AssertNoCompilationErrors(result);
        Assert.GreaterThan(0, result.Output.Length + result.Error.Length, $"{Commands.Watch} command produced no output");
    }
}
