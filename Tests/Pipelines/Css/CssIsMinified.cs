using System;
using System.IO;
using Engine;

using Tests.Framework;
using Tests.Frontend;
using Tests.Pipelines.Html;

namespace Tests.Pipelines.Css;

public sealed class CssIsMinified : ITestCase
{
    public string Name => "Client CSS is minified in dist";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        PageAssetManifest manifest = homePage.Manifest;
        string expectedCssPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(homePage.DirectoryPath, manifest.Css!)
            : Path.Combine(homePage.DirectoryPath, $"{Files.Index}{FileExtensions.Css}");

        Assert.IsTrue(File.Exists(expectedCssPath), "CSS file missing in dist (checked via manifest)");

        string css = File.ReadAllText(expectedCssPath);
        // Allow important license comments (/*! ... */) but disallow any other block comments
        bool hasNonImportantBlockComment = false;
        for (int i = 0; i < css.Length - 3; i++)
        {
            if (css[i] == '/' && css[i + 1] == '*')
            {
                // If it's not an important comment (/*!), flag
                if (i + 2 >= css.Length || css[i + 2] != '!')
                {
                    hasNonImportantBlockComment = true;
                    break;
                }
            }
        }
        Assert.IsFalse(hasNonImportantBlockComment, "Client CSS should not contain non-important block comments after minification");
        Assert.DoesNotContain("  ", css, "Client CSS should be minified with collapsed whitespace");
        Assert.DoesNotContain("\n\n", css, "Client CSS should be minified without extra newlines");
    }
}
