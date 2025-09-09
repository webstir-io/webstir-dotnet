using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Publish;

public sealed class JsIsMinified : ITestCase
{
    public string Name => "Client JS is minified in dist";
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

        string distJs = File.ReadAllText(expectedJsPath);
        Assert.DoesNotContain("/*", distJs, "Client JS should not contain block comments after minification");
        // Allow URLs (http://) and protocols, but basic check to limit line comments in output
        Assert.DoesNotContain("// ", distJs, "Client JS should not contain inline comments after minification");
    }
}
