using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Engine.Bridge.Test;
using Engine.Interfaces;
using Engine.Models;
using Framework.Packaging;
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
    private string? _testRuntimeFilter;
    private ProjectMode? _projectModeFilter;
    private ProjectMode _workspaceMode;

    public override string WorkflowName => Commands.Watch;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        _testRuntimeFilter = TestRuntimeOptionParser.Parse(args);
        _workspaceMode = Context.DetectProjectMode();
        _projectModeFilter = ResolveProjectMode(_testRuntimeFilter);

        ProjectMode? effectiveMode = _projectModeFilter ?? NormalizeWorkspaceMode(_workspaceMode);

        await ExecuteBuildWithFilterAsync(effectiveMode, _workspaceMode);

        PackageEnsureSummary ensureSummary = await TestPackageUtilities.EnsurePackageAsync(Context);
        TestPackageUtilities.LogEnsureMessages(ensureSummary);

        await RunTestsAsync();
        bool watchStarted = false;
        bool frontendWatchEnabled = ShouldStartFrontendWatch(effectiveMode, _workspaceMode);
        try
        {
            if (frontendWatchEnabled)
            {
                await Frontend.StartWatchAsync();
                watchStarted = true;
            }

            await devService.StartAsync(Context, async (filePath, _) =>
            {
                await ExecuteBuildWithFilterAsync(effectiveMode, _workspaceMode, filePath);

                FrontendHotUpdate? hotUpdate = null;
                if (frontendWatchEnabled)
                {
                    FrontendHotUpdate? candidate;
                    while ((candidate = Frontend.DequeueHotUpdate()) is not null)
                    {
                        hotUpdate = candidate;
                    }
                }

                await RunTestsAsync();

                if (!frontendWatchEnabled || hotUpdate is null)
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
            if (frontendWatchEnabled && watchStarted)
            {
                await Frontend.StopWatchAsync();
            }
        }
    }

    private async Task RunTestsAsync()
    {
        TestCliRunner runner = new(Context);
        TestCliRunResult result = await runner.RunTestsAsync(
            CancellationToken.None,
            new TestCliRunSettings(_testRuntimeFilter));

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

    private static ProjectMode? ResolveProjectMode(string? runtimeFilter) =>
        string.Equals(runtimeFilter, "backend", StringComparison.OrdinalIgnoreCase)
            ? ProjectMode.ServerOnly
            : string.Equals(runtimeFilter, "frontend", StringComparison.OrdinalIgnoreCase)
                ? ProjectMode.ClientOnly
                : null;

    private static ProjectMode? NormalizeWorkspaceMode(ProjectMode mode) =>
        mode == ProjectMode.Fullstack ? null : mode;

    private static bool WorkspaceHasFrontend(ProjectMode mode) =>
        mode is ProjectMode.Fullstack or ProjectMode.ClientOnly;

    private async Task ExecuteBuildWithFilterAsync(
        ProjectMode? runtimeMode,
        ProjectMode workspaceMode,
        string? changedFilePath = null)
    {
        ProjectMode? effective = runtimeMode ?? NormalizeWorkspaceMode(workspaceMode);
        if (effective is { } filtered)
        {
            if (changedFilePath is null)
            {
                await ExecuteBuildAsync(filtered);
            }
            else
            {
                await ExecuteBuildAsync(changedFilePath, filtered);
            }

            return;
        }

        if (changedFilePath is null)
        {
            await ExecuteBuildAsync();
        }
        else
        {
            await ExecuteBuildAsync(changedFilePath);
        }
    }

    private static bool ShouldStartFrontendWatch(ProjectMode? runtimeMode, ProjectMode workspaceMode)
    {
        if (!WorkspaceHasFrontend(workspaceMode))
        {
            return false;
        }

        return runtimeMode is null or not ProjectMode.ServerOnly;
    }
}
