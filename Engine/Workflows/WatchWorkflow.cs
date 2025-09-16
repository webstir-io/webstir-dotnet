using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Engine.Pipelines.Test;
using Engine.Services;
using Engine.Interfaces;

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
        await RunTestsAsync();
        await devService.StartAsync(Context, async (filePath, _) =>
        {
            await ExecuteBuildAsync(filePath);
            await RunTestsAsync();
        });
    }

    private async Task RunTestsAsync()
    {
        IEnumerable<string> source = TestDiscovery.FindSourceTests(Context);
        if (!source.Any())
        {
            Console.WriteLine("No tests found under src/**/tests/");
            return;
        }

        IEnumerable<string> compiled = TestDiscovery.MapToCompiled(source, Context);
        RunResult result = await TestRunner.RunAsync(compiled, CancellationToken.None);

        // Minimal inline summary for watch mode
        int passed = result.Passed;
        int failed = result.Failed;
        Console.WriteLine($"Tests: {passed} passed, {failed} failed ({result.Total})");
    }
}
