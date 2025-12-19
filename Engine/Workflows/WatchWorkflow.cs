using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private CancellationTokenSource? _watchTestCancellationSource;
    private Task _watchTestTask = Task.CompletedTask;

    public override string WorkflowName => Commands.Watch;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        _testRuntimeFilter = RuntimeOptionParser.Parse(args);
        _workspaceProfile = WorkspaceProfile;
        WorkspaceProfile effectiveProfile = ApplyRuntimeFilter(_workspaceProfile, _testRuntimeFilter);
        LogRuntimeScope(_workspaceProfile, _testRuntimeFilter, effectiveProfile);

        await ExecuteBuildWithTimingAsync(effectiveProfile, _workspaceProfile);

        PackageEnsureSummary ensureSummary = await TestPackageUtilities.EnsurePackageAsync(Context);
        TestPackageUtilities.LogEnsureMessages(ensureSummary);

        await TypeScriptCompiler.CompileAsync(Context);
        await RunTestsAsync(CancellationToken.None);
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
                await ExecuteBuildWithTimingAsync(effectiveProfile, _workspaceProfile, filePath);

                FrontendHotUpdate? hotUpdate = null;
                if (frontendWatchEnabled)
                {
                    FrontendHotUpdate? candidate;
                    while ((candidate = Frontend.DequeueHotUpdate()) is not null)
                    {
                        hotUpdate = candidate;
                    }
                }

                ScheduleWatchTests();

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

            await StopWatchTestsAsync();
        }
    }

    private void ScheduleWatchTests()
    {
        _watchTestCancellationSource?.Cancel();
        _watchTestCancellationSource?.Dispose();
        _watchTestCancellationSource = new CancellationTokenSource();

        CancellationToken cancellationToken = _watchTestCancellationSource.Token;
        _watchTestTask = Task.Run(async () =>
        {
            try
            {
                await TypeScriptCompiler.CompileAsync(Context);
                await RunTestsAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Watch tests failed.");
            }
        }, cancellationToken);
    }

    private async Task StopWatchTestsAsync()
    {
        if (_watchTestCancellationSource is null)
        {
            return;
        }

        _watchTestCancellationSource.Cancel();
        _watchTestCancellationSource.Dispose();
        _watchTestCancellationSource = null;

        try
        {
            await _watchTestTask;
        }
        catch (OperationCanceledException)
        {
        }

        _watchTestTask = Task.CompletedTask;
    }

    private async Task RunTestsAsync(CancellationToken cancellationToken)
    {
        TestCliRunner runner = new(Context);
        Stopwatch stopwatch = Stopwatch.StartNew();
        TestCliRunResult result = await runner.RunTestsAsync(
            cancellationToken,
            new TestCliRunSettings(_testRuntimeFilter));
        stopwatch.Stop();

        if (!result.TestsDiscovered)
        {
            _logger.LogInformation("No tests found under src/**/tests/");
            return;
        }

        bool succeeded = result.ExitCode == 0 && !result.HadErrors && result.Failed == 0;
        if (succeeded)
        {
            _logger.LogInformation("Testing... done ({Elapsed})", FormatElapsed(stopwatch.ElapsedMilliseconds));
        }
        else
        {
            _logger.LogWarning("Testing... failed ({Elapsed})", FormatElapsed(stopwatch.ElapsedMilliseconds));
        }

        _logger.LogDebug(
            "Testing results. Passed: {Passed}, Failed: {Failed}, Total: {Total}",
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
            ? profile with
            {
                HasFrontend = false,
                HasBackend = true
            }
            : string.Equals(runtimeFilter, "frontend", StringComparison.OrdinalIgnoreCase)
                ? profile with
                {
                    HasFrontend = true,
                    HasBackend = false
                }
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

    private async Task ExecuteBuildWithTimingAsync(
        WorkspaceProfile runtimeProfile,
        WorkspaceProfile workspaceProfile,
        string? changedFilePath = null)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        await ExecuteBuildWithFilterAsync(runtimeProfile, workspaceProfile, changedFilePath);
        stopwatch.Stop();
        _logger.LogInformation("Building... done ({Elapsed})", FormatElapsed(stopwatch.ElapsedMilliseconds));
    }

    private static string FormatElapsed(long elapsedMs)
    {
        if (elapsedMs < 1000)
        {
            return $"{elapsedMs}ms";
        }

        long elapsedTenths = elapsedMs / 100;
        long seconds = elapsedTenths / 10;
        long tenths = elapsedTenths % 10;
        return $"{seconds}.{tenths}s";
    }

    private static bool ShouldStartFrontendWatch(WorkspaceProfile effectiveProfile) =>
        effectiveProfile.HasFrontend;

    private void LogRuntimeScope(WorkspaceProfile workspaceProfile, string? runtimeFilter, WorkspaceProfile effectiveProfile)
    {
        string workspaceLabel = DescribeProfile(workspaceProfile);
        string filterLabel = string.IsNullOrWhiteSpace(runtimeFilter) ? "auto" : runtimeFilter!;
        string effectiveLabel = DescribeProfile(effectiveProfile);

        _logger.LogDebug(
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
