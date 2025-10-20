using System;
using System.IO;
using System.Text.Json;
using Engine;
using Framework.Packaging;
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

        string packageJsonPath = Path.Combine(seedDir, Files.PackageJson);
        Assert.True(File.Exists(packageJsonPath), $"{Files.PackageJson} not found");

        using JsonDocument packageManifest = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        JsonElement dependencies = packageManifest.RootElement.GetProperty("dependencies");
        string expectedSpecifier = Environment.GetEnvironmentVariable("WEBSTIR_TEST_REGISTRY_SPEC")?.Trim()
            ?? FrameworkPackageCatalog.Testing.RegistrySpecifier;
        string actualSpecifier = dependencies.GetProperty("@webstir-io/webstir-testing").GetString() ?? string.Empty;
        Assert.Equal(expectedSpecifier, actualSpecifier);
    }
}
