using System;
using System.IO;
using Engine;
using Tester.Infrastructure;
using Xunit;

namespace Tester.Workflows.Build;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class BackendBuildWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public BackendBuildWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void BuildProducesBackendArtifacts()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping backend build tests: framework packages not available (set GH_PACKAGES_TOKEN).");
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-backend-build";
        string seedDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        string backendBuildRoot = Path.Combine(seedDir, Folders.Build, Folders.Backend);
        if (Directory.Exists(backendBuildRoot))
        {
            Directory.Delete(backendBuildRoot, recursive: true);
        }

        ProcessRunner.ProcessResult result = context.Run(
            $"{Commands.Build} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 30000);

        Assert.False(result.TimedOut, $"{Commands.Build} command timed out");
        Assert.Equal(0, result.ExitCode);
        context.AssertNoCompilationErrors(result);

        string backendIndex = Path.Combine(backendBuildRoot, $"{Files.Index}{FileExtensions.Js}");
        Assert.True(File.Exists(backendIndex), "Backend build index.js not found under build/backend.");
    }
}

