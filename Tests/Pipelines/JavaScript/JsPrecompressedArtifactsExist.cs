using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.JavaScript;

public sealed class JsPrecompressedArtifactsExist : ITestCase
{
    public string Name => "Publish creates .js.br and .js.gz variants";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDirectory = Paths.OutPath;
        string clientPageDirectory = Path.Combine(testDirectory, Folders.Seed, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home);

        Engine.Pipelines.Core.AssetManifest manifest = Engine.Pipelines.Core.AssetManifest.Load(clientPageDirectory);
        string expectedJsPath = !string.IsNullOrWhiteSpace(manifest.Js)
            ? Path.Combine(clientPageDirectory, manifest.Js!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Js}");

        Assert.IsTrue(File.Exists(expectedJsPath), "JS file missing in dist (checked via manifest)");

        string brPath = expectedJsPath + FileExtensions.Br;
        string gzPath = expectedJsPath + FileExtensions.Gz;

        Assert.IsTrue(File.Exists(brPath), ".js.br variant missing next to JS");
        Assert.IsTrue(File.Exists(gzPath), ".js.gz variant missing next to JS");
    }
}

