using System;
using System.IO;
using Engine;
using Tester.Infrastructure;
using Xunit;

namespace Tester.Workflows.Watch;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class WatchWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public WatchWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void WatchStartsAndSignalsReady()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
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
            catch
            {
                // Ignore cleanup failure; watch produces a fresh build.
            }
        }

        ProcessRunner.ProcessResult result = context.Run(
            $"{Commands.Watch} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 12000,
            waitForSignal: App.DevService);

        Assert.True(result.ReceivedReadySignal, "Watch mode did not start - readiness message not received");
        context.AssertNoCompilationErrors(result);
        Assert.True(result.Output.Length + result.Error.Length > 0, "watch command produced no output");
    }
}
