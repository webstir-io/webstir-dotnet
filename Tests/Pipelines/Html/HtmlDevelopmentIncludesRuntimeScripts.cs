using System;
using System.IO;
using Engine;
using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlDevelopmentIncludesRuntimeScripts : ITestCase
{
    public string Name => "Development build includes refresh and HMR scripts";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDirectory = Paths.OutPath;
        Directory.CreateDirectory(testDirectory);
        string projectName = "seed-dev";
        string seedDirectory = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        ProcessRunner.ProcessResult build = context.Cli.Run(
            $"{Commands.Build} {ProjectOptions.ProjectName} {projectName}",
            testDirectory,
            timeoutMs: 20000);
        context.AssertNoCompilationErrors(build);
        Assert.AreEqual(0, build.ExitCode, $"{Commands.Build} command failed. Error: {build.Error}");

        string buildFrontend = Path.Combine(seedDirectory, Folders.Build, Folders.Frontend);
        string refreshRuntimePath = Path.Combine(buildFrontend, Files.RefreshJs);
        string hmrRuntimePath = Path.Combine(buildFrontend, Files.HmrJs);

        Assert.IsTrue(File.Exists(refreshRuntimePath), $"{Files.RefreshJs} missing from development build output");
        Assert.IsTrue(File.Exists(hmrRuntimePath), $"{Files.HmrJs} missing from development build output");

        string pageHtmlPath = Path.Combine(
            buildFrontend,
            Folders.Pages,
            Folders.Home,
            $"{Files.Index}{FileExtensions.Html}");
        Assert.IsTrue(File.Exists(pageHtmlPath), "Development page HTML missing in build output");

        string pageHtml = File.ReadAllText(pageHtmlPath).Replace("\r", string.Empty, StringComparison.Ordinal);
        Assert.Contains("src=\"/refresh.js\"", pageHtml, "Development HTML should reference refresh runtime script");
        Assert.Contains("src=\"/hmr.js\"", pageHtml, "Development HTML should reference HMR runtime script");
    }

}
