using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Publish;

public sealed class ClientArtifactsExist : ITestCase
{
    public string Name => "Publish produces client artifacts in dist";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string testDirectory = Paths.OutPath;
        string distDirectory = Path.Combine(testDirectory, Folders.Seed, Folders.Dist);

        Assert.IsTrue(Directory.Exists(distDirectory), "seed/dist directory does not exist");

        string clientPageDirectory = Path.Combine(distDirectory, Folders.Frontend, Folders.Pages, Folders.Home);
        Assert.IsTrue(
            File.Exists(Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Html}")),
            "client page index.html missing in dist");

        Engine.Pipelines.Core.AssetManifest manifest = Engine.Pipelines.Core.AssetManifest.Load(clientPageDirectory);
        string expectedJsPath = !string.IsNullOrWhiteSpace(manifest.Js)
            ? Path.Combine(clientPageDirectory, manifest.Js!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Js}");

        Assert.IsTrue(File.Exists(expectedJsPath), "client page JS missing in dist (checked via manifest)");
    }
}
