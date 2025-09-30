using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Engine;
using Engine.Bridge;
using Engine.Bridge.Test;
using Engine.Interfaces;
using Framework.Packaging;
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
        bool dryRun = Array.Exists(args, arg => string.Equals(arg, InstallOptions.DryRun, StringComparison.OrdinalIgnoreCase));

        NodeRuntime.EnsureMinimumVersion();
        _logger.LogInformation(dryRun ? "Inspecting framework packages (dry run)..." : "Synchronizing framework packages...");

        PackageWorkspaceAdapter workspaceAdapter = new(Context);
        PackageEnsureSummary summary = await PackageSynchronizer.EnsureAsync(
            workspaceAdapter,
            _logger,
            ensureFrontend: preferRegistry => FrontendPackageInstaller.EnsureAsync(workspaceAdapter, preferRegistry),
            ensureTesting: preferRegistry => TestPackageInstaller.EnsureAsync(workspaceAdapter, preferRegistry),
            includeFrontend: true,
            includeTesting: true,
            autoInstall: !dryRun);

        if (dryRun)
        {
            LogDryRunSummary(summary);
            Environment.ExitCode = summary.InstallRequiredButSkipped || summary.HasVersionMismatch ? 1 : 0;
            return;
        }

        LogFrontendMessages(summary);
        TestPackageUtilities.LogEnsureMessages(summary);

        if (summary.InstallRequiredButSkipped)
        {
            throw new InvalidOperationException($"Framework packages require installation. Run '{App.Name} install' to synchronize dependencies.");
        }

        if (summary.HasVersionMismatch)
        {
            ThrowMismatch(summary);
        }

        _logger.LogInformation("Framework packages are synchronized.");
    }

    private void LogDryRunSummary(PackageEnsureSummary summary)
    {
        bool anyChanges = false;

        LogPackage(summary.Frontend?.Metadata.Name ?? "@electric-coding-llc/webstir-frontend", summary.Frontend);
        LogPackage(summary.Testing?.Metadata.Name ?? "@electric-coding-llc/webstir-test", summary.Testing);

        if (summary.InstallRequiredButSkipped && !anyChanges)
        {
            anyChanges = true;
            _logger.LogInformation("[dry-run] npm install would run due to prior package drift.");
        }

        if (!anyChanges)
        {
            _logger.LogInformation("[dry-run] Framework packages are already synchronized.");
        }

        void LogPackage<TEnsure>(string packageName, TEnsure? result) where TEnsure : struct, IPackageEnsureResult
        {
            if (result is not { } value)
            {
                _logger.LogInformation("[dry-run] {Package} is up to date.", packageName);
                return;
            }

            bool needsInstall = value.ToolsAdded || value.DependencyUpdated || value.TarballUpdated || value.VersionMismatch;
            if (!needsInstall)
            {
                _logger.LogInformation("[dry-run] {Package} is up to date.", packageName);
                return;
            }

            anyChanges = true;
            List<string> reasons = new();
            if (value.ToolsAdded)
            {
                reasons.Add("tools archive added");
            }
            if (value.DependencyUpdated)
            {
                reasons.Add("package.json dependency updated");
            }
            if (value.TarballUpdated)
            {
                reasons.Add("tarball hash changed");
            }
            if (value.VersionMismatch)
            {
                string installed = string.IsNullOrWhiteSpace(value.InstalledVersion) ? "missing" : value.InstalledVersion!;
                reasons.Add($"installed {installed}");
            }

            _logger.LogInformation("[dry-run] {Package} requires npm install ({Reasons}).", packageName, string.Join(", ", reasons));
        }
    }

    private void LogFrontendMessages(PackageEnsureSummary summary)
    {
        if (summary.InstallPerformed)
        {
            _logger.LogInformation("Reinstalled frontend package dependencies.");
        }

        if (summary.Frontend is { TarballUpdated: true } frontend)
        {
            _logger.LogInformation("{Package} tarball updated; dependencies refreshed automatically.", frontend.Metadata.Name);
        }
    }

    private void ThrowMismatch(PackageEnsureSummary summary)
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
