using System;
using System.IO;
using Engine;
using Tests.Pipelines.Html;

using Tests.Framework;
using Tests.Frontend;

namespace Tests.Pipelines.Core;

public sealed class ManifestIntegrity : ITestCase
{
    public string Name => "Asset manifest points to existing files";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HtmlPublishScenarioResult scenario = Tests.Pipelines.Html.HtmlPublishScenarios.HeadCombined(context);
        Tests.Pipelines.Html.HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        string clientPageDirectory = homePage.DirectoryPath;
        PageAssetManifest manifest = homePage.Manifest;

        // JS
        string expectedJsPath = !string.IsNullOrWhiteSpace(manifest.Js)
            ? Path.Combine(clientPageDirectory, manifest.Js!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Js}");
        Assert.IsTrue(File.Exists(expectedJsPath), "Manifest JS path does not exist");

        // CSS
        string expectedCssPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(clientPageDirectory, manifest.Css!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Css}");
        Assert.IsTrue(File.Exists(expectedCssPath), "Manifest CSS path does not exist");

        // HTML
        string expectedHtmlPath = Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Html}");
        Assert.IsTrue(File.Exists(expectedHtmlPath), "Dist HTML path does not exist");
    }
}
