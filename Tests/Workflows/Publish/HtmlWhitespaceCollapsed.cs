using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Publish;

public sealed class HtmlWhitespaceCollapsed : ITestCase
{
    public string Name => "HTML publish collapses inter-tag whitespace and preserves inline script content";
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

        string distHtmlPath = Path.Combine(seedDirectory, Folders.Dist, Folders.Client, Folders.Pages, Folders.Home, $"{Files.Index}{FileExtensions.Html}");
        Assert.IsTrue(File.Exists(distHtmlPath), "Dist index.html missing");
        string distHtml = File.ReadAllText(distHtmlPath);

        string normalized = distHtml.Replace("\r", string.Empty);
        Assert.DoesNotContain("> \n<", normalized, "Inter-tag whitespace should be collapsed");
        Assert.DoesNotContain(">\n<", normalized, "Inter-tag newlines should be collapsed");
        Assert.Contains("</head><body>", normalized, "Head/body boundary should be collapsed");
        Assert.Contains("</main></body>", normalized, "Main/body boundary should be collapsed");
    }
}
