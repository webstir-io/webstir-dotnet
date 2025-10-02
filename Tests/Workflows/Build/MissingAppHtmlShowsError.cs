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
            FileAttributes currentAttributes = File.GetAttributes(appHtml);
            if (currentAttributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(appHtml, currentAttributes & ~FileAttributes.ReadOnly);
            }

            File.Delete(appHtml);
            Assert.IsFalse(File.Exists(appHtml), "Failed to delete app.html before running build.");
        }

        // Use publish here to ensure the HTML pipeline runs and validates the
        // presence of the base app template consistently across platforms.
        ProcessRunner.ProcessResult result = context.Cli.Run(
            $"{Commands.Publish} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 20000);

        Assert.AreEqual(0, result.ExitCode, $"{Commands.Build} command failed. Error: {result.Error}");
        string combined = (result.Output ?? string.Empty) + "\n" + (result.Error ?? string.Empty);
        Assert.IsTrue(
            combined.Contains("Base application HTML file not found", StringComparison.OrdinalIgnoreCase),
            $"Build output should contain an error about missing app.html. Actual output:{Environment.NewLine}{combined}"
        );
    }
}
