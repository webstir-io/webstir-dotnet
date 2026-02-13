using System;
using System.IO;
using Tester.Helpers;
using Tester.Infrastructure;
using Engine;
using Utilities.Process;
using Xunit;
using Xunit.Sdk;

namespace Tester.Pipelines.JavaScript;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class JavaScriptPipelineTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public JavaScriptPipelineTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void JsIsMinified()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping JavaScript pipeline tests: framework packages not available (set NPM_TOKEN).");
        }

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(_fixture.Context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        PageAssetManifest manifest = homePage.Manifest;
        string expectedJsPath = !string.IsNullOrWhiteSpace(manifest.Js)
            ? Path.Combine(homePage.DirectoryPath, manifest.Js!)
            : Path.Combine(homePage.DirectoryPath, $"{Files.Index}{FileExtensions.Js}");

        Assert.True(File.Exists(expectedJsPath), "JS file missing in dist (checked via manifest)");

        string distJs = File.ReadAllText(expectedJsPath);
        Assert.DoesNotContain("/*", distJs, StringComparison.Ordinal);
        Assert.DoesNotContain("// ", distJs, StringComparison.Ordinal);
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void JsTreeShakingRemovesUnusedExports()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-tree";
        string projectDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        string pagesDir = Path.Combine(projectDir, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home);
        string usedTs = Path.Combine(pagesDir, "used.ts");
        string unusedTs = Path.Combine(pagesDir, "unused.ts");
        File.WriteAllText(usedTs, "export function usedFunction123(){ return 'USED_MARK_789'; }\n");
        File.WriteAllText(unusedTs, "export function unusedFunction456(){ return 'UNUSED_MARK_012'; }\n");

        string indexTs = Path.Combine(pagesDir, $"{Files.Index}{FileExtensions.Ts}");
        File.WriteAllText(indexTs, "import { usedFunction123 } from './used';\nwindow.testResult = usedFunction123();\n");

        string buildDir = Path.Combine(projectDir, Folders.Build);
        string distDir = Path.Combine(projectDir, Folders.Dist);
        if (Directory.Exists(buildDir))
        {
            Directory.Delete(buildDir, recursive: true);
        }
        if (Directory.Exists(distDir))
        {
            Directory.Delete(distDir, recursive: true);
        }

        ProcessResult publish = context.Run(
            $"{Commands.Publish} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 45000);
        Assert.Equal(0, publish.ExitCode);
        Assert.False(publish.TimedOut);
        context.AssertNoCompilationErrors(publish);

        string pageDir = Path.Combine(projectDir, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home);
        PageAssetManifest manifest = PageAssetManifest.Load(pageDir);
        string jsPath = !string.IsNullOrWhiteSpace(manifest.Js)
            ? Path.Combine(pageDir, manifest.Js!)
            : Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Js}");
        Assert.True(File.Exists(jsPath), "JS bundle missing in dist (checked via manifest)");

        string bundle = File.ReadAllText(jsPath);
        Assert.Contains("USED_MARK_789", bundle, StringComparison.Ordinal);
        Assert.DoesNotContain("UNUSED_MARK_012", bundle, StringComparison.Ordinal);
        Assert.DoesNotContain("unusedFunction456", bundle, StringComparison.Ordinal);
    }
}
