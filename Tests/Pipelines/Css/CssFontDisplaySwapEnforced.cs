using System;
using System.IO;
using Engine;
using Tests.Framework;

namespace Tests.Pipelines.Css;

public sealed class CssFontDisplaySwapEnforced : ITestCase
{
    public string Name => "@font-face rules include font-display: swap";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        string seedDir = Path.Combine(testDir, Folders.Seed);

        if (!Directory.Exists(Path.Combine(seedDir, Folders.Src)))
        {
            ProcessRunner.ProcessResult init = context.Cli.Run($"{Commands.Init} {ProjectOptions.ProjectName} {Folders.Seed}", testDir, timeoutMs: 15000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} failed: {init.Error}");
        }

        // Add a page that declares a font-face without font-display
        ProcessRunner.ProcessResult addPage = context.Cli.Run($"{Commands.AddPage} fonttest {ProjectOptions.ProjectName} {Folders.Seed}", testDir, timeoutMs: 10000);
        Assert.AreEqual(0, addPage.ExitCode, $"{Commands.AddPage} failed: {addPage.Error}");

        string pageRoot = Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.Pages, "fonttest");
        string cssPath = Path.Combine(pageRoot, $"{Files.Index}{FileExtensions.Css}");
        string css = "@font-face { font-family: 'Demo'; src: url('/fonts/demo.woff2') format('woff2'); }\nbody { font-family: 'Demo', sans-serif; }\n";
        File.WriteAllText(cssPath, css);

        // Publish
        ProcessRunner.ProcessResult publish = context.Cli.Run($"{Commands.Publish} {ProjectOptions.ProjectName} {Folders.Seed}", testDir, timeoutMs: 20000);
        Assert.AreEqual(0, publish.ExitCode, $"{Commands.Publish} failed: {publish.Error}");

        // Read bundled CSS for the page from manifest
        string distPage = Path.Combine(seedDir, Folders.Dist, Folders.Frontend, Folders.Pages, "fonttest");
        Engine.Pipelines.Core.AssetManifest manifest = Engine.Pipelines.Core.AssetManifest.Load(distPage);
        string cssDist = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(distPage, manifest.Css!)
            : Path.Combine(distPage, $"{Files.Index}{FileExtensions.Css}");
        Assert.IsTrue(File.Exists(cssDist), "fonttest CSS missing in dist");
        string bundled = File.ReadAllText(cssDist);

        Assert.Contains("@font-face", bundled, "Expected font-face to be present");
        Assert.Contains("font-display:swap", bundled, "Expected font-display: swap to be inserted");
    }
}
