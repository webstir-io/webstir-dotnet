using System;
using System.IO;
using Engine;
using Tester.Infrastructure;
using Xunit;
using Xunit.Sdk;

namespace Tester.Workflows.Init;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class InitWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public InitWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void InitCommandCreatesDefaultProject()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping init workflow: framework packages not available (set GH_PACKAGES_TOKEN).");
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);

        string seedDir = Path.Combine(testDir, Folders.Seed);
        if (Directory.Exists(seedDir))
        {
            try
            {
                Directory.Delete(seedDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures; subsequent init will overwrite.
            }
        }

        ProcessRunner.ProcessResult result = context.Run(Commands.Init, testDir, timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.App, "app.css")), "app.css missing");
        Assert.True(File.Exists(Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.App, "app.html")), "app.html missing");
        Assert.True(File.Exists(Path.Combine(seedDir, Files.PackageJson)), "package.json missing");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void InitCommandCreatesNamedProject()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping init workflow: framework packages not available (set GH_PACKAGES_TOKEN).");
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        string projectName = "seed-named";
        string namedDir = Path.Combine(testDir, projectName);

        if (Directory.Exists(namedDir))
        {
            try
            {
                Directory.Delete(namedDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures; init overwrites the directory.
            }
        }

        ProcessRunner.ProcessResult result = context.Run(
            $"{Commands.Init} --project-name {projectName}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);
        Assert.True(Directory.Exists(namedDir), "Named project directory not found");
        Assert.True(File.Exists(Path.Combine(namedDir, Folders.Src, Folders.Frontend, Folders.App, "app.css")), "app.css missing");
        Assert.True(File.Exists(Path.Combine(namedDir, Folders.Src, Folders.Frontend, Folders.App, "app.html")), "app.html missing");
        Assert.True(File.Exists(Path.Combine(namedDir, Files.PackageJson)), "package.json missing");
    }
}
