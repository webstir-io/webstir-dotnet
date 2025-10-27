using System;
using System.IO;
using Engine;
using Tester.Infrastructure;
using Xunit;

namespace Tester.Workflows.Publish;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class BackendPublishWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public BackendPublishWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void PublishProducesBackendDist()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping backend publish tests: framework packages not available (set GH_PACKAGES_TOKEN).");
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-backend-publish";
        string seedDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        string backendDistRoot = Path.Combine(seedDir, Folders.Dist, Folders.Backend);
        if (Directory.Exists(backendDistRoot))
        {
            Directory.Delete(backendDistRoot, recursive: true);
        }

        ProcessRunner.ProcessResult result = context.Run(
            $"{Commands.Publish} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 45000);

        Assert.False(result.TimedOut, $"{Commands.Publish} command timed out");
        Assert.Equal(0, result.ExitCode);
        context.AssertNoCompilationErrors(result);

        string backendIndex = Path.Combine(backendDistRoot, $"{Files.Index}{FileExtensions.Js}");
        Assert.True(File.Exists(backendIndex), "Backend dist index.js not found under dist/backend.");
    }
}

