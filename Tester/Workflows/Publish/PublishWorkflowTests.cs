using System;
using System.IO;
using Engine;
using Tester.Infrastructure;
using Utilities.Process;
using Xunit;

namespace Tester.Workflows.Publish;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class PublishWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public PublishWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void PublishRunsWithoutErrors()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping publish tests: framework packages not available (set NPM_TOKEN).");
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-publish";
        string seedDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        string distDir = Path.Combine(seedDir, Folders.Dist);
        if (Directory.Exists(distDir))
        {
            Directory.Delete(distDir, recursive: true);
        }

        ProcessResult result = context.Run(
            $"{Commands.Publish} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 30000);

        Assert.Equal(0, result.ExitCode);
        context.AssertNoCompilationErrors(result);
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void PublishReportsTypeScriptErrors()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-ts-error";
        string projectDir = Path.Combine(testDir, projectName);

        if (Directory.Exists(projectDir))
        {
            try
            {
                Directory.Delete(projectDir, recursive: true);
            }
            catch
            {
                // Skip cleanup failure; init will replace contents.
            }
        }

        ProcessResult init = context.Run(
            $"{Commands.Init} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 10000);
        Assert.Equal(0, init.ExitCode);

        string indexTs = Path.Combine(projectDir, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home, $"{Files.Index}{FileExtensions.Ts}");
        Assert.True(File.Exists(indexTs), $"Expected TS entry at {indexTs}");
        File.AppendAllText(indexTs, "\nconst broken = ;\n");

        ProcessResult publish = context.Run(
            $"{Commands.Publish} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 20000);

        string combinedLower = string.Concat(publish.StandardOutput ?? string.Empty, publish.StandardError ?? string.Empty)
            .ToLowerInvariant();

        Assert.NotEqual(0, publish.ExitCode);
        Assert.Contains("module provider '@webstir-io/webstir-frontend' failed with exit code 1", combinedLower);
        Assert.Contains("unexpected \"", combinedLower);
    }
}
