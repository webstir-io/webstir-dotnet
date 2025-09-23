using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Engine.Bridge.Test;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
using Engine.Services;
using Microsoft.Extensions.Logging;

namespace Engine.Workflows;

public class WatchWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers,
    DevService devService,
    ILogger<WatchWorkflow> logger)
    : BaseWorkflow(context, workers)
{
    private readonly ILogger<WatchWorkflow> _logger = logger;

    public override string WorkflowName => Commands.Watch;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        await ExecuteBuildAsync();

        PackageEnsureResult ensureResult = await TestPackageUtilities.EnsurePackageAsync(Context);
        TestPackageUtilities.LogEnsureMessages(ensureResult);

        await RunTestsAsync();
        bool watchStarted = false;
        try
        {
            await Frontend.StartWatchAsync();
            watchStarted = true;

            await devService.StartAsync(Context, async (filePath, _) =>
            {
                await ExecuteBuildAsync(filePath);

                FrontendHotUpdate? hotUpdate = null;
                FrontendHotUpdate? candidate;
                while ((candidate = Frontend.DequeueHotUpdate()) is not null)
                {
                    hotUpdate = candidate;
                }

                await RunTestsAsync();

                if (hotUpdate is null)
                {
                    return ChangeProcessingResult.Empty;
                }

                return new ChangeProcessingResult
                {
                    HotUpdate = hotUpdate
                };
            });
        }
        finally
        {
            if (watchStarted)
            {
                await Frontend.StopWatchAsync();
            }
        }
    }

    private async Task RunTestsAsync()
    {
        TestCliRunner runner = new(Context);
        TestCliRunResult result = await runner.RunTestsAsync(CancellationToken.None);

        if (!result.TestsDiscovered)
        {
            _logger.LogInformation("No tests found under src/**/tests/");
            return;
        }

        _logger.LogInformation(
            "Tests completed. Passed: {Passed}, Failed: {Failed}, Total: {Total}",
            result.Passed,
            result.Failed,
            result.Total);

        if (result.HadErrors || result.ExitCode != 0)
        {
            _logger.LogWarning("Test runner reported errors. See logs above.");
        }
    }
}
