using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlPrecompressedArtifactsExist : ITestCase
{
    public string Name => "Publish creates .html.br and .html.gz variants";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDirectory = Paths.OutPath;
        string clientPageDirectory = Path.Combine(testDirectory, Folders.Seed, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home);

        string htmlPath = Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Html}");
        Assert.IsTrue(File.Exists(htmlPath), "HTML file missing in dist");

        string brPath = htmlPath + FileExtensions.Br;
        string gzPath = htmlPath + FileExtensions.Gz;

        Assert.IsTrue(File.Exists(brPath), ".html.br variant missing next to HTML");
        Assert.IsTrue(File.Exists(gzPath), ".html.gz variant missing next to HTML");
    }
}

