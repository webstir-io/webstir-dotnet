using System;
using System.IO;
using Tester.Infrastructure;
using Tester.Helpers;
using Engine;
using Engine.Bridge.Frontend;
using Xunit;
using Xunit.Sdk;

namespace Tester.Workflows.Build;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class BuildWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public BuildWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void BuildRunsWithoutErrors()
    {
        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-build";
        string seedDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        string seedBuild = Path.Combine(seedDir, Folders.Build);
        if (Directory.Exists(seedBuild))
        {
            Directory.Delete(seedBuild, recursive: true);
        }

        ProcessRunner.ProcessResult result = context.Run(
            $"{Commands.Build} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 20000);

        Assert.False(result.TimedOut, $"{Commands.Build} command timed out");
        Assert.Equal(0, result.ExitCode);
        context.AssertNoCompilationErrors(result);

        AppWorkspace workspace = new();
        workspace.Initialize(seedDir);

        string frontendRoot = ResolveFrontendRoot(workspace, seedDir);
        Assert.True(Directory.Exists(frontendRoot), "Frontend build/dist directory does not exist");

        string clientPageDir = Path.Combine(frontendRoot, Folders.Pages, Folders.Home);
        Assert.True(File.Exists(Path.Combine(clientPageDir, $"{Files.Index}{FileExtensions.Html}")), "client page index.html missing in build");

        PageAssetManifest pageManifest = PageAssetManifest.Load(clientPageDir);
        string expectedJs = !string.IsNullOrWhiteSpace(pageManifest.Js)
            ? Path.Combine(clientPageDir, pageManifest.Js!)
            : Path.Combine(clientPageDir, $"{Files.Index}{FileExtensions.Js}");
        Assert.True(File.Exists(expectedJs), "Client page JS bundle missing in build");

        if (!string.IsNullOrWhiteSpace(pageManifest.Css))
        {
            string expectedCss = Path.Combine(clientPageDir, pageManifest.Css!);
            Assert.True(File.Exists(expectedCss), "Client page CSS bundle missing in build");
        }
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void MissingAppHtmlShowsError()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-missing-app";
        string projectDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        string appHtml = Path.Combine(projectDir, Folders.Src, Folders.Frontend, Folders.App, "app.html");
        if (File.Exists(appHtml))
        {
            FileAttributes currentAttributes = File.GetAttributes(appHtml);
            if (currentAttributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(appHtml, currentAttributes & ~FileAttributes.ReadOnly);
            }

            File.Delete(appHtml);
            Assert.False(File.Exists(appHtml), "Failed to delete app.html before running build.");
        }

        string pagesHome = Path.Combine(projectDir, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home);
        Directory.CreateDirectory(pagesHome);
        string pageFragment = Path.Combine(pagesHome, $"{Files.Index}{FileExtensions.Html}");
        if (!File.Exists(pageFragment))
        {
            File.WriteAllText(pageFragment, "<head><title>Test</title></head><body><main>Home</main></body>\n");
        }

        ProcessRunner.ProcessResult result = context.Run(
            $"{Commands.Build} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 20000);

        if (result.ExitCode == 0)
        {
            string combined = (result.Output ?? string.Empty) + Environment.NewLine + (result.Error ?? string.Empty);
            throw new XunitException($"Expected non-zero exit code when app.html is missing. Actual output:{Environment.NewLine}{combined}");
        }
    }

    private static string ResolveFrontendRoot(AppWorkspace workspace, string seedRoot)
    {
        try
        {
            FrontendManifest manifest = FrontendManifestLoader.LoadAsync(workspace).GetAwaiter().GetResult();
            string buildRoot = manifest.Paths.Build.Frontend;
            if (Directory.Exists(buildRoot))
            {
                return buildRoot;
            }

            string distRoot = manifest.Paths.Dist.Frontend;
            if (Directory.Exists(distRoot))
            {
                return distRoot;
            }
        }
        catch
        {
            // Ignore manifest issues; fall back to legacy paths below.
        }

        string legacyBuild = Path.Combine(seedRoot, Folders.Build, Folders.Frontend);
        if (Directory.Exists(legacyBuild))
        {
            return legacyBuild;
        }

        string legacyDist = Path.Combine(seedRoot, Folders.Dist, Folders.Frontend);
        return legacyDist;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void BuildWithViteProviderProducesArtifacts()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-vite-provider";
        string seedDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        string providerPath = Path.Combine(Paths.RepositoryRoot, "Framework", "ViteFrontend");
        ProcessRunner.ProcessResult installProvider = ProcessRunner.Run(new ProcessRunOptions
        {
            FileName = "npm",
            Arguments = $"install --no-save \"{providerPath}\"",
            WorkingDirectory = seedDir,
            ExitTimeoutMs = 60000
        });
        Assert.Equal(0, installProvider.ExitCode);

        string configurationPath = Path.Combine(seedDir, "webstir.providers.json");
        File.WriteAllText(configurationPath, """
{
  "frontend": "@webstir-io/webstir-frontend-vite"
}
""");

        string viteConfigPath = Path.Combine(seedDir, "vite.config.ts");
        File.WriteAllText(viteConfigPath, """
import { defineConfig } from 'vite';

export default defineConfig({
  root: 'src/frontend',
  build: {
    outDir: '../../build/frontend',
    emptyOutDir: true,
    lib: {
      entry: 'app/app.ts',
      name: 'WebstirApp',
      formats: ['es']
    }
  }
});
""");
        ProcessRunner.ProcessResult result = context.Run(
            $"{Commands.Build} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 40000);

        Assert.False(result.TimedOut, "Build command timed out while using Vite provider.");
        Assert.Equal(0, result.ExitCode);
        context.AssertNoCompilationErrors(result);
        Assert.Contains("@webstir-io/webstir-frontend-vite", result.Output, StringComparison.OrdinalIgnoreCase);

        string buildRoot = Path.Combine(seedDir, Folders.Build, Folders.Frontend);
        Assert.True(Directory.Exists(buildRoot), "Vite build output directory missing.");

        bool hasBundles = Directory.GetFiles(buildRoot, "*.js", SearchOption.AllDirectories).Length > 0;
        Assert.True(hasBundles, "Vite provider did not emit any JS bundles.");
    }
}
