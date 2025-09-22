using System;
using System.IO;
using Engine;

using Tests.Framework;
using Tests.Frontend;

namespace Tests.Pipelines.Css;

public sealed class CssIsMinified : ITestCase
{
    public string Name => "Client CSS is minified in dist";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDirectory = Paths.OutPath;
        string clientPageDirectory = Path.Combine(testDirectory, Folders.Seed, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home);

        PageAssetManifest manifest = PageAssetManifest.Load(clientPageDirectory);
        string expectedCssPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(clientPageDirectory, manifest.Css!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Css}");

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
