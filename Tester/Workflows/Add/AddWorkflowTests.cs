using System;
using System.IO;
using System.Text.Json;
using Engine;
using Tester.Infrastructure;
using Xunit;

namespace Tester.Workflows.Add;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class AddWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public AddWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void AddPageCreatesFiles()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);
        string seedDir = Path.Combine(testDir, Folders.Seed);

        ProcessRunner.ProcessResult result = context.Run(
            $"{Commands.AddPage} about {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);

        string pageDir = Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.Pages, "about");
        Assert.True(File.Exists(Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Html}")), "index.html not created");
        Assert.True(File.Exists(Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Css}")), "index.css not created");
        Assert.True(File.Exists(Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Ts}")), "index.ts not created");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void AddTestScaffoldsFile()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);
        string seedDir = Path.Combine(testDir, Folders.Seed);

        ProcessRunner.ProcessResult result = context.Run(
            $"{Commands.AddTest} frontend/pages/home/home {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);

        string expectedTest = Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home, Folders.Tests, "home.test.ts");
        Assert.True(File.Exists(expectedTest), $"Test file not created at {expectedTest}");

        string manifestPath = Path.Combine(seedDir, Folders.Webstir, "testing-package.json");
        Assert.True(File.Exists(manifestPath), $"Test package manifest missing at {manifestPath}");

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        string archiveName = manifest.RootElement.GetProperty("fileName").GetString()
            ?? throw new InvalidOperationException("Manifest missing fileName.");
        string dependencyValue = manifest.RootElement.GetProperty("dependency").GetString()
            ?? throw new InvalidOperationException("Manifest missing dependency string.");

        string toolsArchive = Path.Combine(seedDir, Folders.Webstir, archiveName);
        Assert.True(File.Exists(toolsArchive), $"Testing package archive not found at {toolsArchive}");

        string packageJsonPath = Path.Combine(seedDir, Files.PackageJson);
        Assert.True(File.Exists(packageJsonPath), $"{Files.PackageJson} not found");

        string packageJson = File.ReadAllText(packageJsonPath);
        Assert.Contains($"\"@webstir-io/webstir-test\": \"{dependencyValue}\"", packageJson);
    }
}
