using System;
using System.Threading.Tasks;
using Engine.Bridge.Module;
using Engine.Models;
using Framework.Packaging;

namespace Engine.Bridge.Test;

internal static class TestPackageUtilities
{
    internal static async Task<PackageEnsureSummary> EnsurePackageAsync(AppWorkspace workspace)
    {
        NodeRuntime.EnsureMinimumVersion();
        WorkspaceProfile profile = workspace.DetectWorkspaceProfile();
        PackageWorkspaceAdapter workspaceAdapter = new(workspace);
        string testingProviderId = ProviderSpecOverrides.ResolveTestingProviderId(workspace);
        string? testingProviderSpec = ProviderSpecOverrides.GetTestingProviderSpec();
        string? testingDependencyOverride = ProviderSpecOverrides.GetDefaultProviderSpec(
            testingProviderId,
            ProviderSpecOverrides.DefaultTestingProviderId,
            testingProviderSpec);

        string backendProviderId = ProviderSpecOverrides.ResolveBackendProviderId(workspace);
        string? backendProviderSpec = ProviderSpecOverrides.GetBackendProviderSpec();
        string? backendDependencyOverride = ProviderSpecOverrides.GetDefaultProviderSpec(
            backendProviderId,
            ProviderSpecOverrides.DefaultBackendProviderId,
            backendProviderSpec);

        PackageEnsureSummary summary = await PackageSynchronizer.EnsureAsync(
            workspaceAdapter,
            logger: null,
            ensureFrontend: null,
            ensureTesting: () => TestPackageInstaller.EnsureAsync(workspaceAdapter, testingDependencyOverride),
            ensureBackend: () => BackendPackageInstaller.EnsureAsync(workspaceAdapter, backendDependencyOverride),
            includeFrontend: false,
            includeTesting: true,
            includeBackend: profile.HasBackend,
            autoInstall: true);
        await ProviderPackageInstaller.EnsureAsync(
            workspaceAdapter,
            testingProviderId,
            testingProviderSpec,
            ProviderSpecOverrides.DefaultTestingProviderId,
            "testing",
            Console.WriteLine).ConfigureAwait(false);

        if (profile.HasBackend)
        {
            await ProviderPackageInstaller.EnsureAsync(
                workspaceAdapter,
                backendProviderId,
                backendProviderSpec,
                ProviderSpecOverrides.DefaultBackendProviderId,
                "backend",
                Console.WriteLine).ConfigureAwait(false);
        }
        ValidateSummary(summary);
        return summary;
    }

    internal static void LogEnsureMessages(PackageEnsureSummary summary)
    {
        PackageEnsureResult? result = summary.Testing;
        PackageEnsureResult? backend = summary.Backend;

        if (summary.InstallPerformed)
        {
            Console.WriteLine("Reinstalled framework package dependencies.");
        }

        if (summary.InstallRequiredButSkipped)
        {
            Console.WriteLine($"Warning: Framework packages require installation. Run '{App.Name} install' to synchronize dependencies.");
        }

        if (result is null)
        {
            // Continue to report backend details if present.
        }
        else
        {
            if (result.Value.DependencyUpdated)
            {
                Console.WriteLine($"Pinned {result.Value.Metadata.Name} dependency in {Files.PackageJson}");
            }

            if (result.Value.VersionMismatch)
            {
                string installed = string.IsNullOrWhiteSpace(result.Value.InstalledVersion)
                    ? "not installed"
                    : result.Value.InstalledVersion!;
                Console.WriteLine($"Warning: {result.Value.Metadata.Name} {installed} differs from recorded {result.Value.Metadata.Version}. Run '{App.Name} install' to refresh node_modules.");
            }
        }

        if (backend is { DependencyUpdated: true } backendUpdated)
        {
            Console.WriteLine($"Pinned {backendUpdated.Metadata.Name} dependency in {Files.PackageJson}");
        }

        if (backend is { VersionMismatch: true } backendResult)
        {
            string installed = string.IsNullOrWhiteSpace(backendResult.InstalledVersion)
                ? "not installed"
                : backendResult.InstalledVersion!;
            Console.WriteLine($"Warning: {backendResult.Metadata.Name} {installed} differs from recorded {backendResult.Metadata.Version}. Run '{App.Name} install' to refresh node_modules.");
        }
    }

    private static void ValidateSummary(PackageEnsureSummary summary)
    {
        if (summary.InstallRequiredButSkipped)
        {
            throw new InvalidOperationException($"Framework packages require installation. Run '{App.Name} install' to synchronize dependencies.");
        }

        if (summary.Testing is { VersionMismatch: true } testing)
        {
            string installed = string.IsNullOrWhiteSpace(testing.InstalledVersion)
                ? "missing"
                : testing.InstalledVersion!;
            throw new InvalidOperationException(
                $"{testing.Metadata.Name} {installed} detected but {testing.Metadata.Version} is recorded. Run '{App.Name} install' to refresh dependencies.");
        }

        if (summary.Backend is { VersionMismatch: true } backend)
        {
            string installed = string.IsNullOrWhiteSpace(backend.InstalledVersion)
                ? "missing"
                : backend.InstalledVersion!;
            throw new InvalidOperationException(
                $"{backend.Metadata.Name} {installed} detected but {backend.Metadata.Version} is recorded. Run '{App.Name} install' to refresh dependencies.");
        }
    }

}
