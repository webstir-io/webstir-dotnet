using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlHeadOrderingAndAlternate : ITestCase
{
    public string Name => "HTML head orders charset/viewport and dedupes alternate hreflang (page overrides)";
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

        // Edit app template to include alternate hreflangs
        string appHtmlPath = Path.Combine(seedDirectory, Folders.Src, Folders.Frontend, Folders.App, "app.html");
        string appHtml = File.ReadAllText(appHtmlPath);
        appHtml = appHtml.Replace("</head>", "    <link rel=\"alternate\" hreflang=\"en\" href=\"/en/home\" />\n    <link rel=\"alternate\" hreflang=\"fr\" href=\"/fr/home\" />\n</head>");
        File.WriteAllText(appHtmlPath, appHtml);

        // Update page head: override viewport and alternate for 'en'
        string pageDir = Path.Combine(seedDirectory, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home);
        Directory.CreateDirectory(pageDir);
        string pageHtmlPath = Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Html}");
        string pageHtml = """
<head>
    <title>Home</title>
    <meta name="viewport" content="page-viewport">
    <link rel="alternate" hreflang="en" href="/en/home-page" />
    <link rel="stylesheet" href="index.css" />
    <script type="module" src="index.js" async></script>
</head>
<body>
    <main>Home</main>
</body>
""";
        File.WriteAllText(pageHtmlPath, pageHtml);

        // Clean and publish
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

        // One viewport with page content
        Assert.AreEqual(1, CountOccurrences(distHtml, "name=\"viewport\""), "Viewport should be single");
        Assert.Contains("content=\"page-viewport\"", distHtml, "Viewport value should be from page");

        // Alternate hreflang dedup: 'en' overridden by page, 'fr' retained from template
        Assert.AreEqual(1, CountOccurrences(distHtml, "rel=\"alternate\" hreflang=\"en\""), "en alternate should be single");
        Assert.Contains("href=\"/en/home-page\"", distHtml, "en alternate should come from page");
        Assert.AreEqual(1, CountOccurrences(distHtml, "rel=\"alternate\" hreflang=\"fr\""), "fr alternate should be single");
        Assert.Contains("href=\"/fr/home\"", distHtml, "fr alternate should remain from template");

        // Ordering: charset first, viewport early (before <title>)
        int headStart = distHtml.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
        int headEnd = distHtml.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(headStart >= 0 && headEnd > headStart, "Head section should exist");
        string headInner = distHtml.Substring(headStart, headEnd - headStart);
        int charsetPos = headInner.IndexOf("<meta charset=", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(charsetPos >= 0, "Charset meta should exist");
        int viewportPos = headInner.IndexOf("name=\"viewport\"", StringComparison.OrdinalIgnoreCase);
        int titlePos = headInner.IndexOf("<title>", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(viewportPos >= 0 && titlePos >= 0 && viewportPos < titlePos, "Viewport should appear before title");
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

