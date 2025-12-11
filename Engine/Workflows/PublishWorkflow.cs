using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;

namespace Engine.Workflows;

public class PublishWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers) : BaseWorkflow(context, workers)
{
    public override string WorkflowName => Commands.Publish;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        string? runtimeFilter = RuntimeOptionParser.Parse(args);
        string? frontendMode = FrontendModeParser.Parse(args);
        ProjectMode workspaceMode = Context.DetectProjectMode();
        ProjectMode? modeFilter = ResolveProjectMode(runtimeFilter);
        ProjectMode? effectiveMode = modeFilter ?? NormalizeWorkspaceMode(workspaceMode);

        if (effectiveMode is { } filtered)
        {
            await ExecuteBuildAsync(filtered);
        }
        else
        {
            await ExecuteBuildAsync();
        }

        if (!string.IsNullOrWhiteSpace(frontendMode))
        {
            Environment.SetEnvironmentVariable("WEBSTIR_FRONTEND_MODE", frontendMode);
        }

        try
        {
            await ExecuteWorkersAsync(async worker => await worker.PublishAsync(), effectiveMode);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(frontendMode))
            {
                Environment.SetEnvironmentVariable("WEBSTIR_FRONTEND_MODE", null);
            }
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
}
