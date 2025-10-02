using System;
using System.IO;
using Engine;

using Tests.Framework;
using Tests.Frontend;
using Tests.Pipelines.Html;

namespace Tests.Pipelines.Css;

public sealed class CssSeedSnapshot : ITestCase
{
    public string Name => "Seed home CSS matches snapshot";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        PageAssetManifest manifest = homePage.Manifest;
        string cssPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(homePage.DirectoryPath, manifest.Css!)
            : Path.Combine(homePage.DirectoryPath, $"{Files.Index}{FileExtensions.Css}");

        Assert.IsTrue(File.Exists(cssPath), "CSS file missing in dist (checked via manifest)");
        string actual = File.ReadAllText(cssPath);

        // Load snapshot
        string testsRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        string snapshotPath = Path.Combine(testsRoot, "Workflows", "Publish", "__snapshots__", "seed-home-index.css");
        Assert.IsTrue(File.Exists(snapshotPath), $"Snapshot file not found: {snapshotPath}");
        string expected = File.ReadAllText(snapshotPath);

        // Normalize line endings and trailing newline differences for comparison
        static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd();
        Assert.AreEqual(Normalize(expected), Normalize(actual), "Seed home CSS does not match snapshot");
    }
}
