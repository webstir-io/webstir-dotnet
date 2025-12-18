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
        WorkspaceProfile effectiveProfile = ApplyRuntimeFilter(WorkspaceProfile, runtimeFilter);
        string? frontendMode = ResolveFrontendMode(args, effectiveProfile);

        await ExecuteBuildAsync(effectiveProfile);

        string? previousFrontendMode = Environment.GetEnvironmentVariable("WEBSTIR_FRONTEND_MODE");
        if (!string.IsNullOrWhiteSpace(frontendMode))
        {
            Environment.SetEnvironmentVariable("WEBSTIR_FRONTEND_MODE", frontendMode);
        }

        try
        {
            await ExecuteWorkersAsync(async worker => await worker.PublishAsync(), effectiveProfile);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(frontendMode))
            {
                Environment.SetEnvironmentVariable("WEBSTIR_FRONTEND_MODE", previousFrontendMode);
            }
        }
    }

    private static string? ResolveFrontendMode(string[] args, WorkspaceProfile effectiveProfile)
    {
        string? parsed = FrontendModeParser.Parse(args);
        if (!string.IsNullOrWhiteSpace(parsed))
        {
            return parsed;
        }

        if (!effectiveProfile.HasFrontend)
        {
            return null;
        }

        return effectiveProfile.Mode == WorkspaceMode.Ssg
            ? "ssg"
            : null;
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
