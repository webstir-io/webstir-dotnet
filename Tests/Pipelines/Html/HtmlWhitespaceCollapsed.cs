using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlWhitespaceCollapsed : ITestCase
{
    public string Name => "HTML publish keeps readable formatting and preserves inline script content";
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
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");
        }

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

        ProcessRunner.ProcessResult result = context.Cli.Run($"{Commands.Publish} {ProjectOptions.ProjectName} seed", testDirectory, timeoutMs: 15000);
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Publish} command failed. Error: {result.Error}");

        string distHtmlPath = Path.Combine(seedDirectory, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home, $"{Files.Index}{FileExtensions.Html}");
        Assert.IsTrue(File.Exists(distHtmlPath), "Dist index.html missing");
        string distHtml = File.ReadAllText(distHtmlPath);

        Assert.Contains("<html lang=\"en\">\n<head>", distHtml, "Document should start with readable head block");
        Assert.Contains("\n<body>\n\t<main>", distHtml, "Body/main blocks should be expanded on separate lines");
        Assert.Contains("<style data-critical=\"\">", distHtml, "Critical CSS inline style should be present");
        Assert.Contains("<script type=\"module\" src=\"/pages/home/index", distHtml, "Publish should rewrite module script path");
        Assert.Contains("</main>\n</body>\n</html>", distHtml, "Closing tags should retain readable formatting");
    }
}
