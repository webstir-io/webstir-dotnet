using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlMetaPreservation : ITestCase
{
    public string Name => "HTML head preserves and dedupes meta/link canonical; page overrides template";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDirectory = Paths.OutPath;
        Directory.CreateDirectory(testDirectory);
        string seedDirectory = Path.Combine(testDirectory, Folders.Seed);
        if (!Directory.Exists(Path.Combine(seedDirectory, Folders.Src)))
        {
            ProcessRunner.ProcessResult init = context.Cli.Run(Commands.Init, testDirectory, timeoutMs: 10000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} failed. Error: {init.Error}");
        }

        // Modify page fragment to include head overrides and additions
        string pageDir = Path.Combine(seedDirectory, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home);
        Directory.CreateDirectory(pageDir);
        string pageHtmlPath = Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Html}");
        string pageHtml = """
<head>
    <title>Home</title>
    <meta name="viewport" content="page-viewport">
    <meta name="description" content="page-desc">
    <meta property="og:title" content="OG Page Title">
    <link rel="canonical" href="/home" />
    <script data-test="head-script">console.log('in-head');</script>
    <link rel="stylesheet" href="index.css" />
    <script type="module" src="index.js" async></script>
</head>
<body>
    <main>
        Home
    </main>
</body>
""";
        File.WriteAllText(pageHtmlPath, pageHtml);

        // Publish
        string seedBuild = Path.Combine(seedDirectory, Folders.Build);
        string seedDist = Path.Combine(seedDirectory, Folders.Dist);
        if (Directory.Exists(seedBuild))
        {
            try
            {
                Directory.Delete(seedBuild, recursive: true);
            }
            catch { }
        }
        if (Directory.Exists(seedDist))
        {
            try
            {
                Directory.Delete(seedDist, recursive: true);
            }
            catch { }
        }

        ProcessRunner.ProcessResult result = context.Cli.Run($"{Commands.Publish} {ProjectOptions.ProjectName} seed", testDirectory, timeoutMs: 20000);
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Publish} failed. Error: {result.Error}");

        string distHtmlPath = Path.Combine(seedDirectory, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home, $"{Files.Index}{FileExtensions.Html}");
        Assert.IsTrue(File.Exists(distHtmlPath), "Dist index.html missing");
        string distHtml = File.ReadAllText(distHtmlPath);

        // Assertions (accounting for minified HTML without quotes around attributes)
        int viewportCount = CountOccurrences(distHtml, "name=\"viewport\"");
        Assert.AreEqual(1, viewportCount, "Viewport meta should appear exactly once");
        Assert.Contains("content=\"page-viewport\"", distHtml, "Viewport meta should use page value");

        int canonicalCount = CountOccurrences(distHtml, "rel=\"canonical\"");
        Assert.AreEqual(1, canonicalCount, "Canonical link should appear exactly once");
        Assert.Contains("href=\"/home\"", distHtml, "Canonical href should come from page");

        Assert.Contains("name=\"description\"", distHtml, "Description meta should be preserved");
        Assert.Contains("content=\"page-desc\"", distHtml, "Description content should be preserved");
        Assert.Contains("property=\"og:title\"", distHtml, "OG title should be preserved");
        Assert.Contains("content=\"OG Page Title\"", distHtml, "OG title content should be preserved");
        Assert.Contains("data-test=\"head-script\"", distHtml, "Head script from page should be preserved");
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while (true)
        {
            index = text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }
            count++;
            index += value.Length;
        }
        return count;
    }
}
