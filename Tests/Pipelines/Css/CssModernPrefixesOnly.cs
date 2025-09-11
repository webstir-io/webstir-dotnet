using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Css;

public sealed class CssModernPrefixesOnly : ITestCase
{
    public string Name => "CSS modern-only prefixes (no -ms- or legacy flex)";
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

        string pageCss = Path.Combine(seedDirectory, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home, $"{Files.Index}{FileExtensions.Css}");
        string rules = ".f{display:flex}.u{user-select:none}.a{appearance:none}\n";
        File.AppendAllText(pageCss, rules);

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

        string clientPageDirectory = Path.Combine(seedDirectory, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home);
        Engine.Pipelines.Core.AssetManifest manifest = Engine.Pipelines.Core.AssetManifest.Load(clientPageDirectory);
        string expectedCssPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(clientPageDirectory, manifest.Css!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Css}");
        Assert.IsTrue(File.Exists(expectedCssPath), "CSS file missing in dist (checked via manifest)");

        string css = File.ReadAllText(expectedCssPath);
        Assert.DoesNotContain("-ms-", css, "No -ms- prefixes should be present");
        Assert.DoesNotContain("-webkit-box", css, "No legacy -webkit-box flexbox syntax");
        Assert.DoesNotContain("-ms-flexbox", css, "No legacy -ms-flexbox syntax");
        Assert.Contains("-webkit-user-select", css, "Modern -webkit-user-select should be present");
        Assert.Contains("-webkit-appearance", css, "Modern -webkit-appearance should be present");
    }
}

