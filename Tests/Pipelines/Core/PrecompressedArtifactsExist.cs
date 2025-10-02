using System;
using System.IO;
using Engine;
using Tests.Pipelines.Html;

using Tests.Framework;
using Tests.Frontend;

namespace Tests.Pipelines.Core;

public sealed class PrecompressedArtifactsExist : ITestCase
{
    public string Name => "Publish creates .br for HTML/CSS/JS";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Tests.Pipelines.Html.HtmlPublishScenarioResult scenario = Tests.Pipelines.Html.HtmlPublishScenarios.PrecompressionEnabled(context);
        Tests.Pipelines.Html.HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        string clientPageDirectory = homePage.DirectoryPath;

        // HTML
        string htmlPath = Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Html}");
        Assert.IsTrue(File.Exists(htmlPath), "HTML file missing in dist");
        Assert.IsTrue(File.Exists(htmlPath + FileExtensions.Br), ".html.br variant missing next to HTML");

        // CSS (via manifest)
        PageAssetManifest manifest = homePage.Manifest;
        string cssPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(clientPageDirectory, manifest.Css!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Css}");
        Assert.IsTrue(File.Exists(cssPath), "CSS file missing in dist (checked via manifest)");
        Assert.IsTrue(File.Exists(cssPath + FileExtensions.Br), ".css.br variant missing next to CSS");

        // JS (via manifest)
        string jsPath = !string.IsNullOrWhiteSpace(manifest.Js)
            ? Path.Combine(clientPageDirectory, manifest.Js!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Js}");
        Assert.IsTrue(File.Exists(jsPath), "JS file missing in dist (checked via manifest)");
        Assert.IsTrue(File.Exists(jsPath + FileExtensions.Br), ".js.br variant missing next to JS");
    }
}
