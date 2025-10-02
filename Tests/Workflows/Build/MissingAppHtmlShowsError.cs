using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Build;

public sealed class MissingAppHtmlShowsError : ITestCase
{
    public string Name => "Build surfaces error when app.html is missing";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);
        string projectName = "seed-missing-app";
        string projectDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        string appHtml = Path.Combine(projectDir, Folders.Src, Folders.Frontend, Folders.App, "app.html");
        if (File.Exists(appHtml))
        {
            try
            {
                File.Delete(appHtml);
            }
            catch { }
        }

        ProcessRunner.ProcessResult result = context.Cli.Run($"{Commands.Build} {ProjectOptions.ProjectName} {projectName}", testDir, timeoutMs: 10000);

        Assert.AreEqual(0, result.ExitCode, $"{Commands.Build} command failed. Error: {result.Error}");
        string combined = (result.Output ?? string.Empty) + "\n" + (result.Error ?? string.Empty);
        Assert.IsTrue(
            combined.Contains("Base application HTML file not found", StringComparison.OrdinalIgnoreCase),
            "Build output should contain an error about missing app.html"
        );
    }
}
