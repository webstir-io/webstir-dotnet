using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Engine.Helpers;
using Engine.Interfaces;
using Engine.Services;
using Engine.Testing;

namespace Engine.Workflows;

public class WatchWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers,
    DevService devService)
    : BaseWorkflow(context, workers)
{
    public override string WorkflowName => Commands.Watch;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        await ExecuteBuildAsync();

        PackageEnsureResult ensureResult = await TestPackageUtilities.EnsurePackageAsync(Context);
        TestPackageUtilities.LogEnsureMessages(ensureResult);

        await RunTestsAsync();
        await devService.StartAsync(Context, async (filePath, _) =>
        {
            await ExecuteBuildAsync(filePath);
            await RunTestsAsync();
        });
    }

    private async Task RunTestsAsync()
    {
        TestCliRunner runner = new(Context);
        TestCliRunResult result = await runner.RunTestsAsync(CancellationToken.None);

        if (!result.TestsDiscovered)
        {
            Console.WriteLine("No tests found under src/**/tests/");
            return;
        }

        Console.WriteLine($"Tests: {result.Passed} passed, {result.Failed} failed ({result.Total})");

        if (result.HadErrors || result.ExitCode != 0)
        {
            Console.WriteLine("Test runner reported errors. See logs above.");
        }
    }
}
