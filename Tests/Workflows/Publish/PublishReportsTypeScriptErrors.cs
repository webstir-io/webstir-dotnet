using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Publish;

public sealed class PublishReportsTypeScriptErrors : ITestCase
{
    public string Name => "Publish surfaces TypeScript errors in output";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-ts-error";
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

        // Introduce a TypeScript syntax error in the home page entry
        string indexTs = Path.Combine(projectDir, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home, $"{Files.Index}{FileExtensions.Ts}");
        Assert.IsTrue(File.Exists(indexTs), $"Expected TS entry at {indexTs}");
        File.AppendAllText(indexTs, "\nconst broken = ;\n");

        // Run publish
        ProcessRunner.ProcessResult publish = context.Cli.Run($"{Commands.Publish} {ProjectOptions.ProjectName} {projectName}", testDir, timeoutMs: 20000);

        // We expect diagnostics to be reported to console output
        string outputLower = (publish.Output ?? string.Empty).ToLowerInvariant();
        Assert.Contains("js publish diagnostics:", outputLower, "JS Publish diagnostics summary should be printed");
    }
}

