using System;
using System.IO;
using Engine;

using Tests.Framework;
using Tests.Frontend;

namespace Tests.Pipelines.Css;

public sealed class CssSeedSnapshot : ITestCase
{
    public string Name => "Seed home CSS matches snapshot";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-snapshot";
        string projectDir = Path.Combine(testDir, projectName);

        if (Directory.Exists(projectDir))
        {
            try
            {
                Directory.Delete(projectDir, recursive: true);
            }
            catch { }
        }

        // Init a fresh project just for this snapshot
        ProcessRunner.ProcessResult init = context.Cli.Run($"{Commands.Init} {ProjectOptions.ProjectName} {projectName}", testDir, timeoutMs: 10000);
        Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");

        // Publish the project
        ProcessRunner.ProcessResult publish = context.Cli.Run($"{Commands.Publish} {ProjectOptions.ProjectName} {projectName}", testDir, timeoutMs: 15000);
        Assert.AreEqual(0, publish.ExitCode, $"{Commands.Publish} command failed. Error: {publish.Error}");
        context.AssertNoCompilationErrors(publish);

        // Read dist CSS via manifest
        string pageDir = Path.Combine(projectDir, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home);
        PageAssetManifest manifest = PageAssetManifest.Load(pageDir);
        string cssPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(pageDir, manifest.Css!)
            : Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Css}");

        Assert.IsTrue(File.Exists(cssPath), "CSS file missing in dist (checked via manifest)");
        string actual = File.ReadAllText(cssPath);

        // Load snapshot
        string testsRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        string snapshotPath = Path.Combine(testsRoot, "Workflows", "Publish", "__snapshots__", "seed-home-index.css");
        Assert.IsTrue(File.Exists(snapshotPath), $"Snapshot file not found: {snapshotPath}");
        string expected = File.ReadAllText(snapshotPath);

        // Normalize line endings and trailing newline differences for comparison
        static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd();
        Assert.AreEqual(Normalize(expected), Normalize(actual), "Seed home CSS does not match snapshot");
    }
}
