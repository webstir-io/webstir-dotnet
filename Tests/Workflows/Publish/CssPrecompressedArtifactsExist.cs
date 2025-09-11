using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Publish;

public sealed class CssPrecompressedArtifactsExist : ITestCase
{
    public string Name => "Publish creates .css.br and .css.gz variants";
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

        string brPath = expectedCssPath + FileExtensions.Br;
        string gzPath = expectedCssPath + FileExtensions.Gz;

        Assert.IsTrue(File.Exists(brPath), ".css.br variant missing next to CSS");
        Assert.IsTrue(File.Exists(gzPath), ".css.gz variant missing next to CSS");
    }
}

