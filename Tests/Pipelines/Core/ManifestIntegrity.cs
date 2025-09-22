using System;
using System.IO;
using Engine;

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

        string testDirectory = Paths.OutPath;
        string clientPageDirectory = Path.Combine(testDirectory, Folders.Seed, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home);

        PageAssetManifest manifest = PageAssetManifest.Load(clientPageDirectory);

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
