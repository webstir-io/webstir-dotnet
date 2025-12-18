using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Bridge.Backend;
using Engine.Bridge.Module;
using Engine.Interfaces;
using Engine.Models;
using Microsoft.Extensions.Logging;

namespace Engine.Workflows;

public sealed class BackendInspectWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers,
    ILogger<BackendInspectWorkflow> logger) : BaseWorkflow(context, workers)
{
    private readonly ILogger<BackendInspectWorkflow> _logger = logger;

    public override string WorkflowName => Commands.BackendInspect;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        WorkspaceProfile profile = WorkspaceProfile;
        if (!profile.HasBackend)
        {
            _logger.LogWarning("No backend directory detected under src/backend; nothing to inspect.");
            return;
        }

        WorkspaceProfile backendOnly = profile with
        {
            HasFrontend = false,
            HasBackend = true
        };
        await ExecuteBuildAsync(backendOnly);
        await PrintManifestSummaryAsync();
    }

    private async Task PrintManifestSummaryAsync()
    {
        try
        {
            ModuleBuildManifest manifest = await BackendManifestLoader.LoadAsync(Context);
            if (manifest.Module is not { } module)
            {
                _logger.LogInformation("Backend manifest generated but module metadata is missing.");
                return;
            }

            _logger.LogInformation(
                "Backend module: {Name} v{Version} (contract {ContractVersion}).",
                module.Name,
                module.Version,
                module.ContractVersion);

            if (module.Capabilities is { Count: > 0 } capabilities)
            {
                _logger.LogInformation("Capabilities: {Capabilities}.", string.Join(", ", capabilities));
            }
            else
            {
                _logger.LogInformation("Capabilities: none recorded.");
            }

            if (module.Routes is { Count: > 0 } routes)
            {
                string routeSummary = string.Join(", ", routes.Select(r => $"{r.Method.ToUpperInvariant()} {r.Path} ({r.Name})"));
                _logger.LogInformation("Routes ({Count}): {Routes}.", routes.Count, routeSummary);
            }
            else
            {
                _logger.LogInformation("Routes: none recorded.");
            }

            if (module.Jobs is { Count: > 0 } jobs)
            {
                string jobSummary = string.Join(", ", jobs.Select(j =>
                {
                    if (!string.IsNullOrWhiteSpace(j.Schedule))
                    {
                        return $"{j.Name} [{j.Schedule}]";
                    }

                    return j.Name;
                }));

                _logger.LogInformation("Jobs ({Count}): {Jobs}.", jobs.Count, jobSummary);
            }
            else
            {
                _logger.LogInformation("Jobs: none recorded.");
            }
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning(
                "Backend manifest not found. Run '{Command} build --runtime backend' first or ensure the backend provider emitted module metadata.",
                App.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read backend manifest.");
        }
    }
}
