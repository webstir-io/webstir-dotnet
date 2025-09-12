using System;
using System.IO;
using System.Text;
using Engine;
using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlPerfOptimizations : ITestCase
{
    public string Name => "HTML publishes critical CSS, hints, and image enhancements";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        string seedDir = Path.Combine(testDir, Folders.Seed);

        // Ensure seed exists
        if (!Directory.Exists(Path.Combine(seedDir, Folders.Src)))
        {
            ProcessRunner.ProcessResult init = context.Cli.Run($"{Commands.Init} {ProjectOptions.ProjectName} {Folders.Seed}", testDir, timeoutMs: 15000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} failed: {init.Error}");
        }

        // Add a small page to guarantee critical CSS inline size and create anchors/images
        ProcessRunner.ProcessResult addPage = context.Cli.Run($"{Commands.AddPage} perf {ProjectOptions.ProjectName} {Folders.Seed}", testDir, timeoutMs: 10000);
        Assert.AreEqual(0, addPage.ExitCode, $"{Commands.AddPage} failed: {addPage.Error}");

        string pageRoot = Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.Pages, "perf");
        Directory.CreateDirectory(pageRoot);

        // Replace CSS with tiny content to be inlined as critical (<6KB)
        string cssPath = Path.Combine(pageRoot, $"{Files.Index}{FileExtensions.Css}");
        File.WriteAllText(cssPath, "body{color:#123}" + Environment.NewLine);

        // Write a tiny 1x1 PNG to /src/frontend/images and reference it from the page
        string imagesRoot = Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.Images);
        Directory.CreateDirectory(imagesRoot);
        string pngPath = Path.Combine(imagesRoot, "test.png");
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8Xw8AApMBgTF+tYcAAAAASUVORK5CYII=");
        File.WriteAllBytes(pngPath, png);

        // Insert two images and an anchor to /home to trigger prefetch
        string htmlPath = Path.Combine(pageRoot, $"{Files.Index}{FileExtensions.Html}");
        string html = File.ReadAllText(htmlPath);
        StringBuilder sb = new(html.Replace("</main>", string.Empty, StringComparison.Ordinal));
        sb.Append("\n        <img src=\"/images/test.png\" alt=\"a\">\n");
        sb.Append("        <img src=\"/images/test.png\" alt=\"b\">\n");
        sb.Append("        <a href=\"/home\">home</a>\n");
        sb.Append("    </main>\n");
        html = sb.ToString();
        File.WriteAllText(htmlPath, html);

        // Publish
        ProcessRunner.ProcessResult publish = context.Cli.Run($"{Commands.Publish} {ProjectOptions.ProjectName} {Folders.Seed}", testDir, timeoutMs: 20000);
        Assert.AreEqual(0, publish.ExitCode, $"{Commands.Publish} failed: {publish.Error}");

        string distHtmlPath = Path.Combine(seedDir, Folders.Dist, Folders.Frontend, Folders.Pages, "perf", $"{Files.Index}{FileExtensions.Html}");
        Assert.IsTrue(File.Exists(distHtmlPath), "perf page dist HTML missing");
        string distHtml = File.ReadAllText(distHtmlPath).Replace("\r", string.Empty);

        // Critical CSS inlined in head
        Assert.Contains("<style data-critical>", distHtml, "Expected critical CSS inlined in <head>");

        // Preload/modulepreload hints present
        Assert.Contains("rel=preload", distHtml, "Expected preload for CSS");
        Assert.Contains("modulepreload", distHtml, "Expected modulepreload for JS");

        // Prefetch hints present (due to anchor to /home)
        Assert.Contains("rel=prefetch", distHtml, "Expected prefetch hint for next navigation");

        // Image width/height injected and lazy-loading applied on non-first image
        Assert.Contains("width=1 height=1", distHtml, "Expected width/height on images");
        Assert.Contains("loading=lazy", distHtml, "Expected lazy loading on below-the-fold images");
    }
}
