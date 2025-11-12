using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Engine.Interfaces;
using Engine.Models;

namespace Engine.Workflows;

public class BuildWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers)
    : BaseWorkflow(context, workers)
{
    public override string WorkflowName => Commands.Build;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        string? runtimeFilter = RuntimeOptionParser.Parse(args);
        ProjectMode workspaceMode = Context.DetectProjectMode();
        ProjectMode? modeFilter = ResolveProjectMode(runtimeFilter);
        ProjectMode? effectiveMode = modeFilter ?? NormalizeWorkspaceMode(workspaceMode);

        if (effectiveMode is { } filtered)
        {
            await ExecuteBuildAsync(filtered);
            return;
        }

        await ExecuteBuildAsync();
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
