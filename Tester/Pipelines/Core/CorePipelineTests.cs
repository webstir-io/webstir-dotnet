using System;
using System.IO;
using Tester.Helpers;
using Tester.Infrastructure;
using Xunit;
using Xunit.Sdk;
using Engine;

namespace Tester.Pipelines.Core;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class CorePipelineTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public CorePipelineTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void ManifestIntegrity()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping core pipeline tests: framework packages not available (set NPM_TOKEN).");
        }

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(_fixture.Context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        PageAssetManifest manifest = homePage.Manifest;
        string clientPageDirectory = homePage.DirectoryPath;

        string expectedJsPath = !string.IsNullOrWhiteSpace(manifest.Js)
            ? Path.Combine(clientPageDirectory, manifest.Js!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Js}");
        Assert.True(File.Exists(expectedJsPath), "Manifest JS path does not exist");

        string expectedCssPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(clientPageDirectory, manifest.Css!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Css}");
        Assert.True(File.Exists(expectedCssPath), "Manifest CSS path does not exist");

        string expectedHtmlPath = Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Html}");
        Assert.True(File.Exists(expectedHtmlPath), "Dist HTML path does not exist");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void PrecompressedArtifactsExist()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping core pipeline tests: framework packages not available (set NPM_TOKEN).");
        }

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.PrecompressionEnabled(_fixture.Context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        string clientPageDirectory = homePage.DirectoryPath;

        string htmlPath = Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Html}");
        Assert.True(File.Exists(htmlPath), "HTML file missing in dist");
        Assert.True(File.Exists(htmlPath + FileExtensions.Br), ".html.br variant missing next to HTML");

        PageAssetManifest manifest = homePage.Manifest;
        string cssPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(clientPageDirectory, manifest.Css!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Css}");
        Assert.True(File.Exists(cssPath), "CSS file missing in dist (checked via manifest)");
        Assert.True(File.Exists(cssPath + FileExtensions.Br), ".css.br variant missing next to CSS");

        string jsPath = !string.IsNullOrWhiteSpace(manifest.Js)
            ? Path.Combine(clientPageDirectory, manifest.Js!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Js}");
        Assert.True(File.Exists(jsPath), "JS file missing in dist (checked via manifest)");
        Assert.True(File.Exists(jsPath + FileExtensions.Br), ".js.br variant missing next to JS");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void RobotsTxtExists()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping core pipeline tests: framework packages not available (set NPM_TOKEN).");
        }

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(_fixture.Context);
        string robotsPath = Path.Combine(scenario.DistFrontendPath, Files.RobotsTxt);
        Assert.True(File.Exists(robotsPath), "robots.txt missing in dist/frontend");
        string text = File.ReadAllText(robotsPath);
        Assert.Contains("User-agent: *", text, StringComparison.Ordinal);
    }
}
