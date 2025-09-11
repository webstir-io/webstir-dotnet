using System;
using System.IO;
using Engine;

using Tests.Framework;

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
        string projectDir = Path.Combine(testDir, projectName);

        if (Directory.Exists(projectDir))
        {
            try
            {
                Directory.Delete(projectDir, recursive: true);
            }
            catch { }
        }

        // Init a fresh project
        ProcessRunner.ProcessResult init = context.Cli.Run($"{Commands.Init} {ProjectOptions.ProjectName} {projectName}", testDir, timeoutMs: 10000);
        Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");

        // Create modules: used.ts (used) and unused.ts (not imported)
        string pagesDir = Path.Combine(projectDir, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home);
        string usedTs = Path.Combine(pagesDir, "used.ts");
        string unusedTs = Path.Combine(pagesDir, "unused.ts");
        File.WriteAllText(usedTs, "export function used(){ console.log('USED_MARK'); }\n");
        File.WriteAllText(unusedTs, "export function unused(){ console.log('UNUSED_MARK'); }\n");

        // Replace index.ts to import and call only the used symbol
        string indexTs = Path.Combine(pagesDir, $"{Files.Index}{FileExtensions.Ts}");
        File.WriteAllText(indexTs, "import { used } from './used';\nused();\n");

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
        ProcessRunner.ProcessResult publish = context.Cli.Run($"{Commands.Publish} {ProjectOptions.ProjectName} {projectName}", testDir, timeoutMs: 20000);
        Assert.AreEqual(0, publish.ExitCode, $"{Commands.Publish} command failed. Error: {publish.Error}");
        context.AssertNoCompilationErrors(publish);

        // Read bundled JS via manifest
        string pageDir = Path.Combine(projectDir, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home);
        Engine.Pipelines.Core.AssetManifest manifest = Engine.Pipelines.Core.AssetManifest.Load(pageDir);
        string jsPath = !string.IsNullOrWhiteSpace(manifest.Js)
            ? Path.Combine(pageDir, manifest.Js!)
            : Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Js}");
        Assert.IsTrue(File.Exists(jsPath), "JS bundle missing in dist (checked via manifest)");

        string bundle = File.ReadAllText(jsPath);
        Assert.Contains("USED_MARK", bundle, "Used symbol marker should be present in bundle");
        Assert.IsFalse(bundle.Contains("UNUSED_MARK", StringComparison.Ordinal), "Unused symbol marker should have been removed by tree-shaking");
    }
}

