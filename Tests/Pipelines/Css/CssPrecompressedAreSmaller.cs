using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Css;

public sealed class CssPrecompressedAreSmaller : ITestCase
{
    public string Name => "Precompressed CSS is smaller than original";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDirectory = Paths.OutPath;
        string clientPageDirectory = Path.Combine(testDirectory, Folders.Seed, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home);

        Engine.Pipelines.Core.AssetManifest manifest = Engine.Pipelines.Core.AssetManifest.Load(clientPageDirectory);
        string expectedCssPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(clientPageDirectory, manifest.Css!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Css}");

        Assert.IsTrue(File.Exists(expectedCssPath), "CSS file missing in dist (checked via manifest)");

        FileInfo original = new(expectedCssPath);
        FileInfo br = new(expectedCssPath + FileExtensions.Br);

        Assert.IsTrue(br.Exists, ".css.br variant missing next to CSS");

        // Expect Brotli compressed variant to be smaller than the original CSS
        Assert.LessThan(original.Length, br.Length, "Brotli output should be smaller than the original CSS");
    }
}
