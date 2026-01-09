using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Engine.Bridge;
using Engine.Bridge.Module;
using Engine.Bridge.Test;
using Engine.Interfaces;
using Engine.Models;
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
        string? packageManagerOverride = ParsePackageManagerOverride(args);
        string? previousPackageManager = null;
        bool overrideApplied = false;

        if (dryRun && clean)
        {
            throw new InvalidOperationException("--clean cannot be combined with --dry-run.");
        }

        if (!string.IsNullOrWhiteSpace(packageManagerOverride))
        {
            previousPackageManager = Environment.GetEnvironmentVariable(PackageManagerRunner.EnvironmentVariableName);
            Environment.SetEnvironmentVariable(PackageManagerRunner.EnvironmentVariableName, packageManagerOverride);
            overrideApplied = true;
        }

        NodeRuntime.EnsureMinimumVersion();
        _logger.LogInformation(dryRun ? "Inspecting framework packages (dry run)..." : clean ? "Clearing workspace cache and synchronizing framework packages..." : "Synchronizing framework packages...");

        if (clean)
        {
            CleanWorkspaceCache();
        }

        try
        {
            PackageWorkspaceAdapter workspaceAdapter = new(Context);
            WorkspaceProfile profile = WorkspaceProfile;
            PackageManagerDescriptor packageManager = workspaceAdapter.PackageManager;
            string frontendProviderId = ProviderSpecOverrides.ResolveFrontendProviderId(Context);
            string? frontendProviderSpec = ProviderSpecOverrides.GetFrontendProviderSpec();
            string? frontendDependencyOverride = ProviderSpecOverrides.GetDefaultProviderSpec(
                frontendProviderId,
                ProviderSpecOverrides.DefaultFrontendProviderId,
                frontendProviderSpec);

            string testingProviderId = ProviderSpecOverrides.ResolveTestingProviderId(Context);
            string? testingProviderSpec = ProviderSpecOverrides.GetTestingProviderSpec();
            string? testingDependencyOverride = ProviderSpecOverrides.GetDefaultProviderSpec(
                testingProviderId,
                ProviderSpecOverrides.DefaultTestingProviderId,
                testingProviderSpec);

            string backendProviderId = ProviderSpecOverrides.ResolveBackendProviderId(Context);
            string? backendProviderSpec = ProviderSpecOverrides.GetBackendProviderSpec();
            string? backendDependencyOverride = ProviderSpecOverrides.GetDefaultProviderSpec(
                backendProviderId,
                ProviderSpecOverrides.DefaultBackendProviderId,
                backendProviderSpec);

            PackageEnsureSummary summary = await PackageSynchronizer.EnsureAsync(
                workspaceAdapter,
                _logger,
                ensureFrontend: () => FrontendPackageInstaller.EnsureAsync(workspaceAdapter, frontendDependencyOverride),
                ensureTesting: () => TestPackageInstaller.EnsureAsync(workspaceAdapter, testingDependencyOverride),
                ensureBackend: () => BackendPackageInstaller.EnsureAsync(workspaceAdapter, backendDependencyOverride),
                includeFrontend: profile.HasFrontend,
                includeTesting: true,
                includeBackend: profile.HasBackend,
                autoInstall: !dryRun);

            if (dryRun)
            {
                LogDryRunSummary(summary, packageManager);
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

            if (profile.HasFrontend)
            {
                await ProviderPackageInstaller.EnsureAsync(
                    workspaceAdapter,
                    frontendProviderId,
                    frontendProviderSpec,
                    ProviderSpecOverrides.DefaultFrontendProviderId,
                    "frontend",
                    message => _logger.LogInformation(message)).ConfigureAwait(false);
            }

            await ProviderPackageInstaller.EnsureAsync(
                workspaceAdapter,
                testingProviderId,
                testingProviderSpec,
                ProviderSpecOverrides.DefaultTestingProviderId,
                "testing",
                message => _logger.LogInformation(message)).ConfigureAwait(false);

            if (profile.HasBackend)
            {
                await ProviderPackageInstaller.EnsureAsync(
                    workspaceAdapter,
                    backendProviderId,
                    backendProviderSpec,
                    ProviderSpecOverrides.DefaultBackendProviderId,
                    "backend",
                    message => _logger.LogInformation(message)).ConfigureAwait(false);
            }

            _logger.LogInformation("Framework packages are synchronized.");
        }
        finally
        {
            if (overrideApplied)
            {
                Environment.SetEnvironmentVariable(PackageManagerRunner.EnvironmentVariableName, previousPackageManager);
            }
        }
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

    private static string? ParsePackageManagerOverride(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (TryGetOptionValue(arg, InstallOptions.PackageManager, out string? inlineValue) ||
                TryGetOptionValue(arg, InstallOptions.PackageManagerShort, out inlineValue))
            {
                if (!string.IsNullOrWhiteSpace(inlineValue))
                {
                    return inlineValue;
                }

                if (i + 1 >= args.Length)
                {
                    throw new InvalidOperationException($"{InstallOptions.PackageManager} requires a value (npm, pnpm, yarn, optionally with @version).");
                }

                return args[i + 1];
            }
        }

        return null;
    }

    private static bool TryGetOptionValue(string argument, string option, out string? value)
    {
        if (string.Equals(argument, option, StringComparison.OrdinalIgnoreCase))
        {
            value = null;
            return true;
        }

        if (argument.StartsWith(option + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = argument[(option.Length + 1)..];
            return true;
        }

        value = null;
        return false;
    }

    private void LogDryRunSummary(PackageEnsureSummary summary, PackageManagerDescriptor manager)
    {
        bool anyChanges = false;

        LogPackage(summary.Frontend?.Metadata.Name ?? "@webstir-io/webstir-frontend", summary.Frontend);
        LogPackage(summary.Testing?.Metadata.Name ?? "@webstir-io/webstir-testing", summary.Testing);
        LogPackage(summary.Backend?.Metadata.Name ?? "@webstir-io/webstir-backend", summary.Backend);

        if (summary.InstallRequiredButSkipped && !anyChanges)
        {
            anyChanges = true;
            _logger.LogInformation("[dry-run] {Manager} install would run due to prior package drift.", manager.DisplayName);
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

            _logger.LogInformation("[dry-run] {Package} requires {Manager} install ({Reasons}).", packageName, manager.DisplayName, string.Join(", ", reasons));
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
