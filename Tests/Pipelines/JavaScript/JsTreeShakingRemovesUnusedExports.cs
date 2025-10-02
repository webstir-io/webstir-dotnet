using System;
using System.IO;
using Engine;

using Tests.Framework;
using Tests.Frontend;

namespace Tests.Pipelines.JavaScript;

public sealed class JsTreeShakingRemovesUnusedExports : ITestCase
{
    public string Name => "JS tree-shaking removes unused exports";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-tree";
        string projectDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        // Create modules: used.ts (used) and unused.ts (not imported)
        string pagesDir = Path.Combine(projectDir, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home);
        string usedTs = Path.Combine(pagesDir, "used.ts");
        string unusedTs = Path.Combine(pagesDir, "unused.ts");
        // Use unique function names that will survive minification
        File.WriteAllText(usedTs, "export function usedFunction123(){ return 'USED_MARK_789'; }\n");
        File.WriteAllText(unusedTs, "export function unusedFunction456(){ return 'UNUSED_MARK_012'; }\n");

        // Replace index.ts to import and call only the used symbol
        string indexTs = Path.Combine(pagesDir, $"{Files.Index}{FileExtensions.Ts}");
        File.WriteAllText(indexTs, "import { usedFunction123 } from './used';\nwindow.testResult = usedFunction123();\n");

        // Clean outputs if present
        string buildDir = Path.Combine(projectDir, Folders.Build);
        string distDir = Path.Combine(projectDir, Folders.Dist);
        if (Directory.Exists(buildDir))
        {
            try
            {
                Directory.Delete(buildDir, recursive: true);
            }
            catch { }
        }
        if (Directory.Exists(distDir))
        {
            try
            {
                Directory.Delete(distDir, recursive: true);
            }
            catch { }
        }

        // Publish
        ProcessRunner.ProcessResult publish = context.Cli.Run(
            $"{Commands.Publish} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 45000);
        Assert.AreEqual(0, publish.ExitCode, $"{Commands.Publish} command failed. Error: {publish.Error}");
        Assert.IsFalse(publish.TimedOut, $"{Commands.Publish} command timed out for {projectName}");
        context.AssertNoCompilationErrors(publish);

        // Read bundled JS via manifest
        string pageDir = Path.Combine(projectDir, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home);
        PageAssetManifest manifest = PageAssetManifest.Load(pageDir);
        string jsPath = !string.IsNullOrWhiteSpace(manifest.Js)
            ? Path.Combine(pageDir, manifest.Js!)
            : Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Js}");
        Assert.IsTrue(File.Exists(jsPath), "JS bundle missing in dist (checked via manifest)");

        string bundle = File.ReadAllText(jsPath);
        // Check that the used string literal is in the bundle (survives minification)
        Assert.Contains("USED_MARK_789", bundle, "Used function's string literal should be in bundle");
        // Check that the unused module's string literal was tree-shaken away
        Assert.IsFalse(bundle.Contains("UNUSED_MARK_012", StringComparison.Ordinal), "Unused module's string literal should have been removed by tree-shaking");
        // Also verify the unused function name doesn't appear
        Assert.IsFalse(bundle.Contains("unusedFunction456", StringComparison.Ordinal), "Unused function name should have been removed by tree-shaking");
    }
}
