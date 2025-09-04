using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Publish;

public sealed class CssIsMinified : ITestCase
{
    public string Name => "Client CSS is minified in dist";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDirectory = Paths.OutPath;
        string clientPageDirectory = Path.Combine(testDirectory, Folders.Seed, Folders.Dist, Folders.Client, Folders.Pages, Folders.Home);

        Engine.Pipelines.Core.AssetManifest manifest = Engine.Pipelines.Core.AssetManifest.Load(clientPageDirectory);
        string expectedCssPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(clientPageDirectory, manifest.Css!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Css}");

        Assert.IsTrue(File.Exists(expectedCssPath), "CSS file missing in dist (checked via manifest)");

        string css = File.ReadAllText(expectedCssPath);
        Assert.DoesNotContain("/*", css, "Client CSS should not contain block comments after minification");
        Assert.DoesNotContain("  ", css, "Client CSS should be minified with collapsed whitespace");
        Assert.DoesNotContain("\n\n", css, "Client CSS should be minified without extra newlines");
    }
}

