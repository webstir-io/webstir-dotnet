using System;
using System.Threading.Tasks;
using Framework.Packaging;

namespace Engine.Bridge.Test;

internal static class TestPackageUtilities
{
    internal static async Task<PackageEnsureSummary> EnsurePackageAsync(AppWorkspace workspace)
    {
        NodeRuntime.EnsureMinimumVersion();
        PackageWorkspaceAdapter workspaceAdapter = new(workspace);
        PackageEnsureSummary summary = await PackageSynchronizer.EnsureAsync(
            workspaceAdapter,
            logger: null,
            ensureFrontend: null,
            ensureTesting: preferRegistry => TestPackageInstaller.EnsureAsync(workspaceAdapter, preferRegistry),
            includeFrontend: false,
            includeTesting: true,
            autoInstall: true);
        ValidateSummary(summary);
        return summary;
    }

    internal static void LogEnsureMessages(PackageEnsureSummary summary)
    {
        PackageEnsureResult? result = summary.Testing;

        if (summary.InstallPerformed)
        {
            Console.WriteLine("Reinstalled @electric-coding-llc/webstir-test dependencies.");
        }

        if (summary.InstallRequiredButSkipped)
        {
            Console.WriteLine($"Warning: Framework packages require installation. Run '{App.Name} install' to synchronize dependencies.");
        }

        if (result is null)
        {
            return;
        }

        if (result.Value.DependencyUpdated)
        {
            Console.WriteLine($"Pinned @electric-coding-llc/webstir-test dependency in {Files.PackageJson}");
        }

        if (result.Value.VersionMismatch)
        {
            string installed = string.IsNullOrWhiteSpace(result.Value.InstalledVersion)
                ? "not installed"
                : result.Value.InstalledVersion!;
            Console.WriteLine($"Warning: @electric-coding-llc/webstir-test {installed} differs from packaged {result.Value.Metadata.Version}. Run '{App.Name} install' to refresh node_modules.");
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
                $"@electric-coding-llc/webstir-test {installed} detected but {testing.Metadata.Version} is bundled. Run '{App.Name} install' to refresh dependencies.");
        }
    }
}
