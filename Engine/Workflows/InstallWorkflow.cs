using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Engine;
using Engine.Bridge;
using Engine.Bridge.Packages;
using Engine.Bridge.Test;
using Engine.Interfaces;
using Microsoft.Extensions.Logging;

namespace Engine.Workflows;

public sealed class InstallWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers,
    ILogger<InstallWorkflow> logger)
    : BaseWorkflow(context, workers)
{
    private readonly ILogger<InstallWorkflow> _logger = logger;

    public override string WorkflowName => Commands.Install;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        NodeRuntime.EnsureMinimumVersion();
        _logger.LogInformation("Synchronizing framework packages...");

        ToolchainEnsureSummary summary = await ToolchainSynchronizer.EnsureAsync(
            Context,
            _logger,
            includeFrontend: true,
            includeTesting: true,
            autoInstall: true);

        LogFrontendMessages(summary);
        TestPackageUtilities.LogEnsureMessages(summary);

        if (summary.InstallRequiredButSkipped)
        {
            throw new InvalidOperationException($"Framework toolchain requires installation. Run '{App.Name} install' to synchronize dependencies.");
        }

        if (summary.HasVersionMismatch)
        {
            ThrowMismatch(summary);
        }

        _logger.LogInformation("Framework packages are synchronized.");
    }

    private void LogFrontendMessages(ToolchainEnsureSummary summary)
    {
        if (summary.InstallPerformed)
        {
            _logger.LogInformation("Reinstalled frontend toolchain dependencies.");
        }

        if (summary.Frontend is { TarballUpdated: true } frontend)
        {
            _logger.LogInformation("{Package} tarball updated; dependencies refreshed automatically.", frontend.Metadata.Name);
        }
    }

    private void ThrowMismatch(ToolchainEnsureSummary summary)
    {
        List<string> mismatches = [];

        if (summary.Frontend is { VersionMismatch: true } frontend)
        {
            string installed = string.IsNullOrWhiteSpace(frontend.InstalledVersion)
                ? "missing"
                : frontend.InstalledVersion!;
            _logger.LogWarning(
                "{Package} {InstalledVersion} detected but {ExpectedVersion} is bundled. Run '{Command} install' to refresh dependencies.",
                frontend.Metadata.Name,
                installed,
                frontend.Metadata.Version,
                App.Name);
            mismatches.Add($"{frontend.Metadata.Name} (found {installed}, expected {frontend.Metadata.Version})");
        }

        if (summary.Testing is { VersionMismatch: true } testing)
        {
            string installed = string.IsNullOrWhiteSpace(testing.InstalledVersion)
                ? "missing"
                : testing.InstalledVersion!;
            _logger.LogWarning(
                "{Package} {InstalledVersion} detected but {ExpectedVersion} is bundled. Run '{Command} install' to refresh dependencies.",
                testing.Metadata.Name,
                installed,
                testing.Metadata.Version,
                App.Name);
            mismatches.Add($"{testing.Metadata.Name} (found {installed}, expected {testing.Metadata.Version})");
        }

        if (mismatches.Count == 0)
        {
            return;
        }

        string details = string.Join(", ", mismatches);
        throw new InvalidOperationException($"Framework packages are out of sync: {details}. Run '{App.Name} install' to synchronize dependencies.");
    }
}
