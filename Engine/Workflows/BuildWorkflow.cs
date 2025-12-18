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
        WorkspaceProfile effectiveProfile = ApplyRuntimeFilter(WorkspaceProfile, runtimeFilter);
        await ExecuteBuildAsync(effectiveProfile);
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
}
