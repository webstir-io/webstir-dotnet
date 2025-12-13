using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Engine.Bridge.Test;
using Engine.Helpers;
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
    private WorkspaceProfile _workspaceProfile;

    public override string WorkflowName => Commands.Watch;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        _testRuntimeFilter = RuntimeOptionParser.Parse(args);
        _workspaceProfile = WorkspaceProfile;
        WorkspaceProfile effectiveProfile = ApplyRuntimeFilter(_workspaceProfile, _testRuntimeFilter);
        LogRuntimeScope(_workspaceProfile, _testRuntimeFilter, effectiveProfile);

        await ExecuteBuildWithFilterAsync(effectiveProfile, _workspaceProfile);

        PackageEnsureSummary ensureSummary = await TestPackageUtilities.EnsurePackageAsync(Context);
        TestPackageUtilities.LogEnsureMessages(ensureSummary);

        await TypeScriptCompiler.CompileAsync(Context);
        await RunTestsAsync();
        bool watchStarted = false;
        bool frontendWatchEnabled = ShouldStartFrontendWatch(effectiveProfile);
        try
        {
            if (frontendWatchEnabled)
            {
                await Frontend.StartWatchAsync();
                watchStarted = true;
            }

            await devService.StartAsync(Context, async (filePath, _) =>
            {
                await ExecuteBuildWithFilterAsync(effectiveProfile, _workspaceProfile, filePath);

                FrontendHotUpdate? hotUpdate = null;
                if (frontendWatchEnabled)
                {
                    FrontendHotUpdate? candidate;
                    while ((candidate = Frontend.DequeueHotUpdate()) is not null)
                    {
                        hotUpdate = candidate;
                    }
                }

                await TypeScriptCompiler.CompileAsync(Context);
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

    private static WorkspaceProfile ApplyRuntimeFilter(WorkspaceProfile profile, string? runtimeFilter) =>
        string.Equals(runtimeFilter, "backend", StringComparison.OrdinalIgnoreCase)
            ? profile with { HasFrontend = false, HasBackend = true }
            : string.Equals(runtimeFilter, "frontend", StringComparison.OrdinalIgnoreCase)
                ? profile with { HasFrontend = true, HasBackend = false }
                : profile;

    private async Task ExecuteBuildWithFilterAsync(
        WorkspaceProfile runtimeProfile,
        WorkspaceProfile workspaceProfile,
        string? changedFilePath = null)
    {
        WorkspaceProfile effective = runtimeProfile;
        if (changedFilePath is null)
        {
            await ExecuteBuildAsync(effective);
        }
        else
        {
            await ExecuteBuildAsync(changedFilePath, effective);
        }
    }

    private static bool ShouldStartFrontendWatch(WorkspaceProfile effectiveProfile) =>
        effectiveProfile.HasFrontend;

    private void LogRuntimeScope(WorkspaceProfile workspaceProfile, string? runtimeFilter, WorkspaceProfile effectiveProfile)
    {
        string workspaceLabel = DescribeProfile(workspaceProfile);
        string filterLabel = string.IsNullOrWhiteSpace(runtimeFilter) ? "auto" : runtimeFilter!;
        string effectiveLabel = DescribeProfile(effectiveProfile);

        _logger.LogInformation(
            "[{Workflow}] Runtime scope — workspace: {Workspace}, filter: {Filter}, running: {Effective}.",
            WorkflowName,
            workspaceLabel,
            filterLabel,
            effectiveLabel);
    }

    private static string DescribeProfile(WorkspaceProfile profile) => (profile.HasFrontend, profile.HasBackend) switch
    {
        (true, true) => "frontend+backend",
        (true, false) => "frontend-only",
        (false, true) => "backend-only",
        _ => "none"
    };
}
