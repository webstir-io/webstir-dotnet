using System;
using System.IO;
using Engine;
using Tests.Framework;
using Tests.Frontend;

namespace Tests.Pipelines.Html;

public sealed class HtmlFeatureFlagsRespectDisables : ITestCase
{
    public string Name => "HTML publish respects disabled feature flags";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.FeatureFlagsDisabled(context);
        context.AssertNoCompilationErrors(scenario.PublishResult);
        Assert.AreEqual(0, scenario.PublishResult.ExitCode, $"{Commands.Publish} failed: {scenario.PublishResult.Error}");

        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        string distHtml = homePage.HtmlNormalized;

        Assert.DoesNotContain("data-critical", distHtml, "Critical CSS should not be inlined when htmlSecurity is disabled.");
        Assert.Contains("rel=\"stylesheet\"", distHtml, "Stylesheet link should remain when htmlSecurity is disabled.");
        Assert.DoesNotContain("width=\"", distHtml, "Image width should not be injected when image optimization is disabled.");
        Assert.DoesNotContain("height=\"", distHtml, "Image height should not be injected when image optimization is disabled.");

        string distHtmlPath = homePage.HtmlPath;
        Assert.IsFalse(File.Exists(distHtmlPath + FileExtensions.Br), "HTML .br variant should not exist when precompression is disabled.");
        Assert.IsFalse(File.Exists(distHtmlPath + FileExtensions.Gz), "HTML .gz variant should not exist when precompression is disabled.");

        PageAssetManifest manifest = homePage.Manifest;

        string cssDistPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(homePage.DirectoryPath, manifest.Css!)
            : Path.Combine(homePage.DirectoryPath, $"{Files.Index}{FileExtensions.Css}");
        string jsDistPath = !string.IsNullOrWhiteSpace(manifest.Js)
            ? Path.Combine(homePage.DirectoryPath, manifest.Js!)
            : Path.Combine(homePage.DirectoryPath, $"{Files.Index}{FileExtensions.Js}");

        Assert.IsTrue(File.Exists(cssDistPath), "Dist CSS missing for feature flags page");
        Assert.IsTrue(File.Exists(jsDistPath), "Dist JS missing for feature flags page");

        Assert.IsFalse(File.Exists(cssDistPath + FileExtensions.Br), "CSS .br variant should not exist when precompression is disabled.");
        Assert.IsFalse(File.Exists(cssDistPath + FileExtensions.Gz), "CSS .gz variant should not exist when precompression is disabled.");

        Assert.IsFalse(File.Exists(jsDistPath + FileExtensions.Br), "JS .br variant should not exist when precompression is disabled.");
        Assert.IsFalse(File.Exists(jsDistPath + FileExtensions.Gz), "JS .gz variant should not exist when precompression is disabled.");
    }
}
