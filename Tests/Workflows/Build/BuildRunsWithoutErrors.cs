using System;
using System.IO;
using Engine;
using Engine.Bridge.Frontend;

using Tests.Framework;
using Tests.Frontend;

namespace Tests.Workflows.Build;

public sealed class BuildRunsWithoutErrors : ITestCase
{
    public string Name => "Build command runs without compilation errors";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);
        string projectName = "seed-build";
        string seedDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        // Clean previous build
        string seedBuild = Path.Combine(seedDir, Folders.Build);
        if (Directory.Exists(seedBuild))
        {
            Directory.Delete(seedBuild, recursive: true);
        }

        ProcessRunner.ProcessResult result = context.Cli.Run($"{Commands.Build} {ProjectOptions.ProjectName} {projectName}", testDir, timeoutMs: 20000);

        if (result.TimedOut)
        {
            Assert.Fail($"{Commands.Build} command timed out");
        }

        Assert.AreEqual(0, result.ExitCode, $"{Commands.Build} command failed. Error: {result.Error}");
        context.AssertNoCompilationErrors(result);

        // Verify minimal expected artifacts
        AppWorkspace workspace = new();
        workspace.Initialize(seedDir);

        string frontendRoot = ResolveFrontendRoot(workspace, seedDir);
        Assert.IsTrue(Directory.Exists(frontendRoot), "Frontend build/dist directory does not exist");

        string clientPageDir = Path.Combine(frontendRoot, Folders.Pages, Folders.Home);
        Assert.IsTrue(File.Exists(Path.Combine(clientPageDir, $"{Files.Index}{FileExtensions.Html}")), "client page index.html missing in build");

        PageAssetManifest pageManifest = PageAssetManifest.Load(clientPageDir);
        string expectedJs = !string.IsNullOrWhiteSpace(pageManifest.Js)
            ? Path.Combine(clientPageDir, pageManifest.Js!)
            : Path.Combine(clientPageDir, $"{Files.Index}{FileExtensions.Js}");
        Assert.IsTrue(File.Exists(expectedJs), "Client page JS bundle missing in build");

        if (!string.IsNullOrWhiteSpace(pageManifest.Css))
        {
            string expectedCss = Path.Combine(clientPageDir, pageManifest.Css!);
            Assert.IsTrue(File.Exists(expectedCss), "Client page CSS bundle missing in build");
        }
    }

    private static string ResolveFrontendRoot(AppWorkspace workspace, string seedRoot)
    {
        try
        {
            FrontendManifest manifest = FrontendManifestLoader.LoadAsync(workspace).GetAwaiter().GetResult();
            string buildRoot = manifest.Paths.Build.Frontend;
            if (Directory.Exists(buildRoot))
            {
                return buildRoot;
            }

            string distRoot = manifest.Paths.Dist.Frontend;
            if (Directory.Exists(distRoot))
            {
                return distRoot;
            }
        }
        catch
        {
            // Ignore manifest issues; fall back to legacy paths below.
        }

        string legacyBuild = Path.Combine(seedRoot, Folders.Build, Folders.Frontend);
        if (Directory.Exists(legacyBuild))
        {
            return legacyBuild;
        }

        string legacyDist = Path.Combine(seedRoot, Folders.Dist, Folders.Frontend);
        return legacyDist;
    }
}
