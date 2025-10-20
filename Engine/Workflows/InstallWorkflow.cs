using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        bool clean = Array.Exists(args, arg => string.Equals(arg, InstallOptions.Clean, StringComparison.OrdinalIgnoreCase));

        if (dryRun && clean)
        {
            throw new InvalidOperationException("--clean cannot be combined with --dry-run.");
        }

        NodeRuntime.EnsureMinimumVersion();
        _logger.LogInformation(dryRun ? "Inspecting framework packages (dry run)..." : clean ? "Clearing workspace cache and synchronizing framework packages..." : "Synchronizing framework packages...");

        if (clean)
        {
            CleanWorkspaceCache();
        }

        PackageWorkspaceAdapter workspaceAdapter = new(Context);
        PackageEnsureSummary summary = await PackageSynchronizer.EnsureAsync(
            workspaceAdapter,
            _logger,
            ensureFrontend: () => FrontendPackageInstaller.EnsureAsync(workspaceAdapter),
            ensureTesting: () => TestPackageInstaller.EnsureAsync(workspaceAdapter),
            ensureBackend: () => BackendPackageInstaller.EnsureAsync(workspaceAdapter),
            includeFrontend: true,
            includeTesting: true,
            includeBackend: true,
            autoInstall: !dryRun);

        if (dryRun)
        {
            LogDryRunSummary(summary);
            Environment.ExitCode = summary.InstallRequiredButSkipped || summary.HasVersionMismatch ? 1 : 0;
            return;
        }

        LogPackageMessages(summary);
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

    private void CleanWorkspaceCache()
    {
        string webstirPath = Context.WebstirPath;
        if (!Directory.Exists(webstirPath))
        {
            _logger.LogInformation("No workspace cache found under {Path}.", webstirPath);
            return;
        }

        try
        {
            Directory.Delete(webstirPath, recursive: true);
            _logger.LogInformation("Removed workspace cache at {Path}.", webstirPath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to clear workspace cache at {Path}.", webstirPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Insufficient permissions to clear workspace cache at {Path}.", webstirPath);
        }
    }

    private void LogDryRunSummary(PackageEnsureSummary summary)
    {
        bool anyChanges = false;

        LogPackage(summary.Frontend?.Metadata.Name ?? "@webstir-io/webstir-frontend", summary.Frontend);
        LogPackage(summary.Testing?.Metadata.Name ?? "@webstir-io/webstir-testing", summary.Testing);
        LogPackage(summary.Backend?.Metadata.Name ?? "@webstir-io/webstir-backend", summary.Backend);

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

            bool needsInstall = value.DependencyUpdated || value.VersionMismatch;
            if (!needsInstall)
            {
                _logger.LogInformation("[dry-run] {Package} is up to date.", packageName);
                return;
            }

            anyChanges = true;
            List<string> reasons = new();
            if (value.DependencyUpdated)
            {
                reasons.Add("package.json dependency updated");
            }
            if (value.VersionMismatch)
            {
                string installed = string.IsNullOrWhiteSpace(value.InstalledVersion) ? "missing" : value.InstalledVersion!;
                reasons.Add($"installed {installed}");
            }

            _logger.LogInformation("[dry-run] {Package} requires npm install ({Reasons}).", packageName, string.Join(", ", reasons));
        }
    }

    private void LogPackageMessages(PackageEnsureSummary summary)
    {
        if (summary.InstallPerformed)
        {
            _logger.LogInformation("Reinstalled framework package dependencies.");
        }

        LogPackageDependency(summary.Frontend);
        LogPackageDependency(summary.Backend);
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

        if (summary.Backend is { VersionMismatch: true } backend)
        {
            string installed = string.IsNullOrWhiteSpace(backend.InstalledVersion)
                ? "missing"
                : backend.InstalledVersion!;
            _logger.LogWarning(
                "{Package} {InstalledVersion} detected but {ExpectedVersion} is bundled. Run '{Command} install' to refresh dependencies.",
                backend.Metadata.Name,
                installed,
                backend.Metadata.Version,
                App.Name);
            mismatches.Add($"{backend.Metadata.Name} (found {installed}, expected {backend.Metadata.Version})");
        }

        if (mismatches.Count == 0)
        {
            return;
        }

        string details = string.Join(", ", mismatches);
        throw new InvalidOperationException($"Framework packages are out of sync: {details}. Run '{App.Name} install' to synchronize dependencies.");
    }

    private void LogPackageDependency(FrontendPackageEnsureResult? result)
    {
        if (result is not { DependencyUpdated: true } dependency)
        {
            return;
        }

        _logger.LogInformation("{Package} dependency updated to match bundled registry metadata.", dependency.Metadata.Name);
    }

    private void LogPackageDependency(PackageEnsureResult? result)
    {
        if (result is not { DependencyUpdated: true } dependency)
        {
            return;
        }

        _logger.LogInformation("{Package} dependency updated to match bundled registry metadata.", dependency.Metadata.Name);
    }
}
