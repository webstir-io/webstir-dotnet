using System;
using System.IO;
using Engine;
using Tests.Framework;
using Tests.Frontend;

namespace Tests.Pipelines.Html;

public sealed class HtmlFeatureFlagsRespectDisables : ITestCase
{
    public string Name => "HTML publish respects disabled feature flags";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDirectory = Paths.OutPath;
        string projectName = "feature-flags";
        string projectDirectory = Path.Combine(testDirectory, projectName);

        if (Directory.Exists(projectDirectory))
        {
            Directory.Delete(projectDirectory, recursive: true);
        }

        ProcessRunner.ProcessResult init = context.Cli.Run(
            $"{Commands.Init} {ProjectOptions.ProjectName} {projectName}",
            testDirectory,
            timeoutMs: 20000);
        Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} failed: {init.Error}");

        ProcessRunner.ProcessResult npmInstall = ProcessRunner.Run(new ProcessRunOptions
        {
            FileName = "npm",
            Arguments = "install",
            WorkingDirectory = projectDirectory,
            ExitTimeoutMs = 60000
        });
        Assert.AreEqual(0, npmInstall.ExitCode, $"npm install failed: {npmInstall.Error}");

        string frontendRoot = Path.Combine(projectDirectory, Folders.Src, Folders.Frontend);
        string pagesHomeDir = Path.Combine(frontendRoot, Folders.Pages, Folders.Home);
        Directory.CreateDirectory(pagesHomeDir);

        string imagesRoot = Path.Combine(frontendRoot, Folders.Images);
        Directory.CreateDirectory(imagesRoot);

        string pngPath = Path.Combine(imagesRoot, "test.png");
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8Xw8AApMBgTF+tYcAAAAASUVORK5CYII=");
        File.WriteAllBytes(pngPath, png);

        string cssPath = Path.Combine(pagesHomeDir, $"{Files.Index}{FileExtensions.Css}");
        File.WriteAllText(cssPath, "main{padding:4px}\n");

        string htmlPath = Path.Combine(pagesHomeDir, $"{Files.Index}{FileExtensions.Html}");
        string html = """
<head>
    <title>Feature Flags</title>
    <link rel="stylesheet" href="index.css" />
    <script type="module" src="index.js"></script>
</head>
<body>
    <main>
        <img src="/images/test.png" alt="primary" />
        <img src="/images/test.png" alt="secondary" />
    </main>
</body>
""";
        File.WriteAllText(htmlPath, html);

        string configPath = Path.Combine(frontendRoot, "frontend.config.json");
        string config = """
{
  "htmlSecurity": false,
  "imageOptimization": false,
  "precompression": false
}
""";
        File.WriteAllText(configPath, config);

        ProcessRunner.ProcessResult publish = context.Cli.Run(
            $"{Commands.Publish} {ProjectOptions.ProjectName} {projectName}",
            testDirectory,
            timeoutMs: 25000);
        context.AssertNoCompilationErrors(publish);
        Assert.AreEqual(0, publish.ExitCode, $"{Commands.Publish} failed: {publish.Error}");

        string distPageDir = Path.Combine(projectDirectory, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home);
        string distHtmlPath = Path.Combine(distPageDir, $"{Files.Index}{FileExtensions.Html}");
        Assert.IsTrue(File.Exists(distHtmlPath), "Dist HTML missing for feature flags page");

        string distHtml = File.ReadAllText(distHtmlPath).Replace("\r", string.Empty, StringComparison.Ordinal);

        Assert.DoesNotContain("data-critical", distHtml, "Critical CSS should not be inlined when htmlSecurity is disabled.");
        Assert.Contains("rel=\"stylesheet\"", distHtml, "Stylesheet link should remain when htmlSecurity is disabled.");
        Assert.DoesNotContain("width=\"", distHtml, "Image width should not be injected when image optimization is disabled.");
        Assert.DoesNotContain("height=\"", distHtml, "Image height should not be injected when image optimization is disabled.");

        Assert.IsFalse(File.Exists(distHtmlPath + FileExtensions.Br), "HTML .br variant should not exist when precompression is disabled.");
        Assert.IsFalse(File.Exists(distHtmlPath + FileExtensions.Gz), "HTML .gz variant should not exist when precompression is disabled.");

        PageAssetManifest manifest = PageAssetManifest.Load(distPageDir);

        string cssDistPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(distPageDir, manifest.Css!)
            : Path.Combine(distPageDir, $"{Files.Index}{FileExtensions.Css}");
        string jsDistPath = !string.IsNullOrWhiteSpace(manifest.Js)
            ? Path.Combine(distPageDir, manifest.Js!)
            : Path.Combine(distPageDir, $"{Files.Index}{FileExtensions.Js}");

        Assert.IsTrue(File.Exists(cssDistPath), "Dist CSS missing for feature flags page");
        Assert.IsTrue(File.Exists(jsDistPath), "Dist JS missing for feature flags page");

        Assert.IsFalse(File.Exists(cssDistPath + FileExtensions.Br), "CSS .br variant should not exist when precompression is disabled.");
        Assert.IsFalse(File.Exists(cssDistPath + FileExtensions.Gz), "CSS .gz variant should not exist when precompression is disabled.");

        Assert.IsFalse(File.Exists(jsDistPath + FileExtensions.Br), "JS .br variant should not exist when precompression is disabled.");
        Assert.IsFalse(File.Exists(jsDistPath + FileExtensions.Gz), "JS .gz variant should not exist when precompression is disabled.");
    }
}
