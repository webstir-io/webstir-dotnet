using System;
using System.IO;
using System.Text.Json;
using Engine;
using Tester.Infrastructure;
using Utilities.Process;
using Xunit;
using Xunit.Sdk;

namespace Tester.Workflows.Init;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class InitWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    private const string FRONTEND_PACKAGE = "@webstir-io/webstir-frontend";
    private const string BACKEND_PACKAGE = "@webstir-io/webstir-backend";
    private const string TESTING_PACKAGE = "@webstir-io/webstir-testing";

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
            throw new ConditionalSkipException("Skipping init workflow: framework packages not available (set NPM_TOKEN).");
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

        ProcessResult result = context.Run(Commands.Init, testDir, timeoutMs: 10000);

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
            throw new ConditionalSkipException("Skipping init workflow: framework packages not available (set NPM_TOKEN).");
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

        ProcessResult result = context.Run(
            $"{Commands.Init} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);
        Assert.True(Directory.Exists(namedDir), "Named project directory not found");
        Assert.True(File.Exists(Path.Combine(namedDir, Folders.Src, Folders.Frontend, Folders.App, "app.css")), "app.css missing");
        Assert.True(File.Exists(Path.Combine(namedDir, Folders.Src, Folders.Frontend, Folders.App, "app.html")), "app.html missing");
        Assert.True(File.Exists(Path.Combine(namedDir, Files.PackageJson)), "package.json missing");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void InitCommandCreatesModeSpecificProjects()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping init workflow: framework packages not available (set NPM_TOKEN).");
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);

        InitScenario[] scenarios =
        [
            new InitScenario(
                InitModes.Ssg,
                "seed-ssg",
                ExpectFrontend: true,
                ExpectBackend: false,
                ExpectedDependencies: [FRONTEND_PACKAGE, TESTING_PACKAGE],
                UnexpectedDependencies: [BACKEND_PACKAGE]),
            new InitScenario(
                InitModes.Spa,
                "seed-spa",
                ExpectFrontend: true,
                ExpectBackend: false,
                ExpectedDependencies: [FRONTEND_PACKAGE, TESTING_PACKAGE],
                UnexpectedDependencies: [BACKEND_PACKAGE]),
            new InitScenario(
                InitModes.Api,
                "seed-api",
                ExpectFrontend: false,
                ExpectBackend: true,
                ExpectedDependencies: [BACKEND_PACKAGE, TESTING_PACKAGE],
                UnexpectedDependencies: [FRONTEND_PACKAGE])
        ];

        foreach (InitScenario scenario in scenarios)
        {
            string projectDir = Path.Combine(testDir, scenario.ProjectFolder);
            if (Directory.Exists(projectDir))
            {
                try
                {
                    Directory.Delete(projectDir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup failures; init overwrites the directory.
                }
            }

            ProcessResult initResult = context.Run(
                $"{Commands.Init} {scenario.Mode} {scenario.ProjectFolder}",
                testDir,
                timeoutMs: 10000);

            Assert.Equal(0, initResult.ExitCode);
            AssertModeFolders(projectDir, scenario);
            AssertModeDependencies(projectDir, scenario);

            if (string.Equals(scenario.Mode, InitModes.Ssg, StringComparison.Ordinal))
            {
                string pagesRoot = Path.Combine(projectDir, Folders.Src, Folders.Frontend, Folders.Pages);
                string contentRoot = Path.Combine(projectDir, Folders.Src, Folders.Frontend, Folders.Content);
                Assert.True(File.Exists(Path.Combine(pagesRoot, "home", Files.IndexHtml)), "SSG home page missing");
                Assert.True(File.Exists(Path.Combine(pagesRoot, "about", Files.IndexHtml)), "SSG about page missing");
                Assert.True(File.Exists(Path.Combine(contentRoot, "content-pipeline.md")), "SSG docs sample missing");
            }
        }
    }

    private static void AssertModeFolders(string projectDir, InitScenario scenario)
    {
        string frontendPath = Path.Combine(projectDir, Folders.Src, Folders.Frontend);
        string backendPath = Path.Combine(projectDir, Folders.Src, Folders.Backend);

        Assert.Equal(scenario.ExpectFrontend, Directory.Exists(frontendPath));
        Assert.Equal(scenario.ExpectBackend, Directory.Exists(backendPath));
    }

    private static void AssertModeDependencies(string projectDir, InitScenario scenario)
    {
        string packageJsonPath = Path.Combine(projectDir, Files.PackageJson);
        Assert.True(File.Exists(packageJsonPath), "package.json missing");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        JsonElement dependencies = document.RootElement.GetProperty("dependencies");

        foreach (string expectedDependency in scenario.ExpectedDependencies)
        {
            Assert.True(
                dependencies.TryGetProperty(expectedDependency, out _),
                $"Expected dependency '{expectedDependency}' missing for mode '{scenario.Mode}'.");
        }

        foreach (string unexpectedDependency in scenario.UnexpectedDependencies)
        {
            Assert.False(
                dependencies.TryGetProperty(unexpectedDependency, out _),
                $"Unexpected dependency '{unexpectedDependency}' present for mode '{scenario.Mode}'.");
        }
    }

    private readonly record struct InitScenario(
        string Mode,
        string ProjectFolder,
        bool ExpectFrontend,
        bool ExpectBackend,
        string[] ExpectedDependencies,
        string[] UnexpectedDependencies);
}
