using System;
using System.IO;
using Engine;

using Tests.Framework;
using Tests.Frontend;
using Tests.Pipelines.Html;

namespace Tests.Pipelines.JavaScript;

public sealed class JsIsMinified : ITestCase
{
    public string Name => "Client JS is minified in dist";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        PageAssetManifest manifest = homePage.Manifest;
        string expectedJsPath = !string.IsNullOrWhiteSpace(manifest.Js)
            ? Path.Combine(homePage.DirectoryPath, manifest.Js!)
            : Path.Combine(homePage.DirectoryPath, $"{Files.Index}{FileExtensions.Js}");

        Assert.IsTrue(File.Exists(expectedJsPath), "JS file missing in dist (checked via manifest)");

        string distJs = File.ReadAllText(expectedJsPath);
        Assert.DoesNotContain("/*", distJs, "Client JS should not contain block comments after minification");
        // Allow URLs (http://) and protocols, but basic check to limit line comments in output
        Assert.DoesNotContain("// ", distJs, "Client JS should not contain inline comments after minification");
    }
}
