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

        // Ensure a page fragment exists so the HTML builder engages
        string pagesHome = Path.Combine(projectDir, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home);
        Directory.CreateDirectory(pagesHome);
        string pageFragment = Path.Combine(pagesHome, $"{Files.Index}{FileExtensions.Html}");
        if (!File.Exists(pageFragment))
        {
            File.WriteAllText(pageFragment, "<head><title>Test</title></head><body><main>Home</main></body>\n");
        }

        // Run build to trigger the missing template check in the HTML builder
        ProcessRunner.ProcessResult result = context.Cli.Run(
            $"{Commands.Build} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 20000);

        // When app.html is missing, the pipeline should fail and surface the template error. 
        if (result.ExitCode == 0)
        {
            string combined = (result.Output ?? string.Empty) + "\n" + (result.Error ?? string.Empty);
            Assert.Fail(
                $"Expected non-zero exit code when app.html is missing. Actual output:{Environment.NewLine}{combined}");
        }
    }
}
