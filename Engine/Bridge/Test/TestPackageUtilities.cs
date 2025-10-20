using System;
using System.IO;
using System.Threading.Tasks;
using Framework.Packaging;

namespace Engine.Bridge.Test;

internal static class TestPackageUtilities
{
    private const string DefaultProviderId = "@webstir-io/webstir-testing";
    private const string ProviderOverrideVariable = "WEBSTIR_TESTING_PROVIDER";
    private const string ProviderSpecVariable = "WEBSTIR_TESTING_PROVIDER_SPEC";

    internal static async Task<PackageEnsureSummary> EnsurePackageAsync(AppWorkspace workspace)
    {
        NodeRuntime.EnsureMinimumVersion();
        PackageWorkspaceAdapter workspaceAdapter = new(workspace);
        PackageEnsureSummary summary = await PackageSynchronizer.EnsureAsync(
            workspaceAdapter,
            logger: null,
            ensureFrontend: null,
            ensureTesting: () => TestPackageInstaller.EnsureAsync(workspaceAdapter),
            ensureBackend: () => BackendPackageInstaller.EnsureAsync(workspaceAdapter),
            includeFrontend: false,
            includeTesting: true,
            includeBackend: true,
            autoInstall: true);
        await EnsureAlternateProviderAsync(workspaceAdapter).ConfigureAwait(false);
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
                Console.WriteLine($"Pinned @webstir-io/webstir-testing dependency in {Files.PackageJson}");
            }

            if (result.Value.VersionMismatch)
            {
                string installed = string.IsNullOrWhiteSpace(result.Value.InstalledVersion)
                    ? "not installed"
                    : result.Value.InstalledVersion!;
                Console.WriteLine($"Warning: @webstir-io/webstir-testing {installed} differs from packaged {result.Value.Metadata.Version}. Run '{App.Name} install' to refresh node_modules.");
            }
        }

        if (backend is { DependencyUpdated: true })
        {
            Console.WriteLine($"Pinned @webstir-io/webstir-backend dependency in {Files.PackageJson}");
        }

        if (backend is { VersionMismatch: true } backendResult)
        {
            string installed = string.IsNullOrWhiteSpace(backendResult.InstalledVersion)
                ? "not installed"
                : backendResult.InstalledVersion!;
            Console.WriteLine($"Warning: @webstir-io/webstir-backend {installed} differs from packaged {backendResult.Metadata.Version}. Run '{App.Name} install' to refresh node_modules.");
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
                $"@webstir-io/webstir-testing {installed} detected but {testing.Metadata.Version} is bundled. Run '{App.Name} install' to refresh dependencies.");
        }

        if (summary.Backend is { VersionMismatch: true } backend)
        {
            string installed = string.IsNullOrWhiteSpace(backend.InstalledVersion)
                ? "missing"
                : backend.InstalledVersion!;
            throw new InvalidOperationException(
                $"@webstir-io/webstir-backend {installed} detected but {backend.Metadata.Version} is bundled. Run '{App.Name} install' to refresh dependencies.");
        }
    }

    private static async Task EnsureAlternateProviderAsync(IPackageWorkspace workspace)
    {
        string? overrideId = Environment.GetEnvironmentVariable(ProviderOverrideVariable);
        if (string.IsNullOrWhiteSpace(overrideId) || string.Equals(overrideId, DefaultProviderId, StringComparison.Ordinal))
        {
            return;
        }

        if (IsPackagePresent(workspace, overrideId))
        {
            return;
        }

        string? overrideSpec = Environment.GetEnvironmentVariable(ProviderSpecVariable);
        string installSpec = string.IsNullOrWhiteSpace(overrideSpec) ? overrideId : overrideSpec;

        Console.WriteLine($"[packages] Installing testing provider override '{installSpec}'.");
        await workspace.InstallPackagesAsync(installSpec).ConfigureAwait(false);
    }

    private static bool IsPackagePresent(IPackageWorkspace workspace, string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return false;
        }

        string nodeModules = workspace.NodeModulesPath;
        if (!Directory.Exists(nodeModules))
        {
            return false;
        }

        if (!packageName.StartsWith("@", StringComparison.Ordinal))
        {
            string packagePath = Path.Combine(nodeModules, packageName);
            return Directory.Exists(packagePath);
        }

        string[] segments = packageName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            return false;
        }

        string scopedPath = Path.Combine(nodeModules, segments[0], segments[1]);
        return Directory.Exists(scopedPath);
    }
}
