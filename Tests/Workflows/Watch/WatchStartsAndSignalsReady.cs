using Engine;

using Tests.Framework;

namespace Tests.Workflows.Watch;

public sealed class WatchStartsAndSignalsReady : ITestCase
{
    public string Name => "Watch command starts and signals ready";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);
        string seedDir = Path.Combine(testDir, Folders.Seed);
        if (!Directory.Exists(Path.Combine(seedDir, Folders.Src)))
        {
            ProcessRunner.ProcessResult init = context.Cli.Run(Commands.Init, testDir, timeoutMs: 10000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");
        }

        string seedBuild = Path.Combine(seedDir, Folders.Build);
        if (Directory.Exists(seedBuild))
        {
            try
            {
                Directory.Delete(seedBuild, recursive: true);
            }
            catch { }
        }

        ProcessRunner.ProcessResult result = context.Cli.Run(
            $"{Commands.Watch} {ProjectOptions.ProjectName} seed",
            testDir,
            timeoutMs: 12000,
            waitForSignal: App.DevService
        );

        Assert.IsTrue(result.ReceivedReadySignal, "Watch mode did not start - readiness message not received");
        context.AssertNoCompilationErrors(result);
        Assert.GreaterThan(0, result.Output.Length + result.Error.Length, $"{Commands.Watch} command produced no output");
    }
}

