using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Css;

public sealed class CssMinifierInvariants : ITestCase
{
    public string Name => "CSS minifier preserves strings/URLs and normalizes zeros";
    public TestCategory Category => TestCategory.Quick;

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

        // Append CSS that exercises invariants
        string pageCss = Path.Combine(seedDirectory, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home, $"{Files.Index}{FileExtensions.Css}");
        string invariantCss = "\n/* Invariant fixtures */\n.str::after{content:\" : ; {} \"}\n.icon{background-image:url(data:image/svg+xml;utf8,<svg viewBox='0 0 1 1'></svg>)}\n.bg{background-image:url(/images/my icon.png)}\n.zero{margin:0rem 0px 0% 0vh}\n";
        File.AppendAllText(pageCss, invariantCss);

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

        ProcessRunner.ProcessResult result = context.Cli.Run($"{Commands.Publish} {ProjectOptions.ProjectName} seed", testDirectory, timeoutMs: 15000);
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Publish} command failed. Error: {result.Error}");
        context.AssertNoCompilationErrors(result);

        // Read dist CSS
        string clientPageDirectory = Path.Combine(seedDirectory, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home);
        Engine.Pipelines.Core.AssetManifest manifest = Engine.Pipelines.Core.AssetManifest.Load(clientPageDirectory);
        string expectedCssPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(clientPageDirectory, manifest.Css!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Css}");
        Assert.IsTrue(File.Exists(expectedCssPath), "CSS file missing in dist (checked via manifest)");

        string css = File.ReadAllText(expectedCssPath);

        // Strings preserved
        Assert.Contains("content:\" : ; {} \"", css, "String literal content should be preserved");

        // data: URIs left untouched
        Assert.Contains("url(data:image/svg+xml;utf8,<svg viewBox='0 0 1 1'></svg>)", css, "data URI should remain unchanged");

        // url with space gets quoted
        Assert.Contains("url(\"/images/my icon.png\")", css, "URL with spaces should be quoted");

        // (Zero shorthand collapse is covered in CssZeroShorthandCollapse)
    }
}
