using System;
using System.IO;
using Engine;
using Tester.Infrastructure;
using Utilities.Process;
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
            throw new ConditionalSkipException("Skipping backend publish tests: framework packages not available (set NPM_TOKEN).");
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

        ProcessResult result = context.Run(
            $"{Commands.Publish} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 45000);

        Assert.False(result.TimedOut, $"{Commands.Publish} command timed out");
        Assert.Equal(0, result.ExitCode);
        context.AssertNoCompilationErrors(result);

        string backendIndex = Path.Combine(backendDistRoot, $"{Files.Index}{FileExtensions.Js}");
        Assert.True(File.Exists(backendIndex), "Backend dist index.js not found under dist/backend.");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void PublishEmitsSourceMapsWhenEnabled()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping backend publish tests: framework packages not available (set NPM_TOKEN).");
        }

        string? prev = Environment.GetEnvironmentVariable("WEBSTIR_BACKEND_SOURCEMAPS");
        Environment.SetEnvironmentVariable("WEBSTIR_BACKEND_SOURCEMAPS", "on");
        try
        {
            TestCaseContext context = _fixture.Context;
            string testDir = context.OutPath;
            Directory.CreateDirectory(testDir);

            string projectName = "seed-backend-publish-maps";
            string seedDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

            string backendDistRoot = Path.Combine(seedDir, Folders.Dist, Folders.Backend);
            if (Directory.Exists(backendDistRoot))
            {
                Directory.Delete(backendDistRoot, recursive: true);
            }

            ProcessResult result = context.Run(
                $"{Commands.Publish} {ProjectOptions.ProjectName} {projectName}",
                testDir,
                timeoutMs: 45000);

            Assert.False(result.TimedOut, $"{Commands.Publish} command timed out");
            Assert.Equal(0, result.ExitCode);
            context.AssertNoCompilationErrors(result);

            string backendIndex = Path.Combine(backendDistRoot, $"{Files.Index}{FileExtensions.Js}");
            string backendMap = Path.Combine(backendDistRoot, $"{Files.Index}{FileExtensions.Js}{FileExtensions.Map}");
            Assert.True(File.Exists(backendIndex), "Backend dist index.js not found under dist/backend.");
            Assert.True(File.Exists(backendMap), "Backend dist index.js.map not found under dist/backend with sourcemaps on.");

            // Verify a sourceMappingURL comment remains when maps are enabled
            string js = File.ReadAllText(backendIndex);
            Assert.Contains("sourceMappingURL=", js, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSTIR_BACKEND_SOURCEMAPS", prev);
        }
    }
}
