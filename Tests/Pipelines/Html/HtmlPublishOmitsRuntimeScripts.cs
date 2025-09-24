using System;
using System.IO;
using Engine;
using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlPublishOmitsRuntimeScripts : ITestCase
{
    public string Name => "Publish output omits development runtime scripts";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDirectory = Paths.OutPath;
        Directory.CreateDirectory(testDirectory);
        string seedDirectory = Path.Combine(testDirectory, Folders.Seed);
        EnsureSeedProject(context, testDirectory, seedDirectory);

        ProcessRunner.ProcessResult publish = context.Cli.Run(
            $"{Commands.Publish} {ProjectOptions.ProjectName} seed",
            testDirectory,
            timeoutMs: 20000);
        context.AssertNoCompilationErrors(publish);
        Assert.AreEqual(0, publish.ExitCode, $"{Commands.Publish} command failed. Error: {publish.Error}");

        string distFrontend = Path.Combine(seedDirectory, Folders.Dist, Folders.Frontend);
        string refreshRuntimePath = Path.Combine(distFrontend, Files.RefreshJs);
        string hmrRuntimePath = Path.Combine(distFrontend, Files.HmrJs);

        Assert.IsFalse(File.Exists(refreshRuntimePath), $"{Files.RefreshJs} should not be emitted in publish output");
        Assert.IsFalse(File.Exists(hmrRuntimePath), $"{Files.HmrJs} should not be emitted in publish output");

        string distHtmlPath = Path.Combine(
            distFrontend,
            Folders.Pages,
            Folders.Home,
            $"{Files.Index}{FileExtensions.Html}");
        Assert.IsTrue(File.Exists(distHtmlPath), "Published home page HTML missing");

        string distHtml = File.ReadAllText(distHtmlPath).Replace("\r", string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("/refresh.js", distHtml, "Published HTML should not reference refresh runtime script");
        Assert.DoesNotContain("/hmr.js", distHtml, "Published HTML should not reference HMR runtime script");
    }

    private static void EnsureSeedProject(TestCaseContext context, string testDirectory, string seedDirectory)
    {
        if (Directory.Exists(Path.Combine(seedDirectory, Folders.Src)))
        {
            return;
        }

        ProcessRunner.ProcessResult init = context.Cli.Run(
            $"{Commands.Init} {ProjectOptions.ProjectName} seed",
            testDirectory,
            timeoutMs: 15000);
        Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} failed: {init.Error}");
    }
}
