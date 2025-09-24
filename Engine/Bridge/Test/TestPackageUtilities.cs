using System;
using System.IO;
using System.Threading.Tasks;

using Engine;
using Engine.Bridge.Packages;

namespace Engine.Bridge.Test;

internal static class TestPackageUtilities
{
    internal static async Task<ToolchainEnsureSummary> EnsurePackageAsync(AppWorkspace workspace)
    {
        NodeRuntime.EnsureMinimumVersion();
        ToolchainEnsureSummary summary = await ToolchainSynchronizer.EnsureAsync(
            workspace,
            logger: null,
            includeFrontend: false,
            includeTesting: true,
            autoInstall: true);
        ValidateSummary(summary);
        return summary;
    }

    internal static void LogEnsureMessages(ToolchainEnsureSummary summary)
    {
        PackageEnsureResult? result = summary.Testing;

        if (summary.InstallPerformed)
        {
            Console.WriteLine("Reinstalled @webstir/test dependencies.");
        }

        if (summary.InstallRequiredButSkipped)
        {
            Console.WriteLine($"Warning: Framework toolchain requires installation. Run '{App.Name} install' to synchronize dependencies.");
        }

        if (result is null)
        {
            return;
        }

        if (result.Value.ToolsAdded)
        {
            Console.WriteLine($"Added testing package archive: {Path.Combine(Folders.Tools, result.Value.Metadata.FileName)}");
        }

        if (result.Value.DependencyUpdated)
        {
            Console.WriteLine($"Pinned @webstir/test to {result.Value.Metadata.Dependency} in {Files.PackageJson}");
        }

        if (result.Value.TarballUpdated && !summary.InstallPerformed)
        {
            Console.WriteLine("Updated @webstir/test tarball; run 'npm install' if changes are not applied automatically.");
        }

        if (result.Value.VersionMismatch)
        {
            string installed = string.IsNullOrWhiteSpace(result.Value.InstalledVersion)
                ? "not installed"
                : result.Value.InstalledVersion!;
            Console.WriteLine($"Warning: @webstir/test {installed} differs from packaged {result.Value.Metadata.Version}. Run '{App.Name} install' to refresh node_modules.");
        }
    }

    private static void ValidateSummary(ToolchainEnsureSummary summary)
    {
        if (summary.InstallRequiredButSkipped)
        {
            throw new InvalidOperationException($"Framework toolchain requires installation. Run '{App.Name} install' to synchronize dependencies.");
        }

        if (summary.Testing is { VersionMismatch: true } testing)
        {
            string installed = string.IsNullOrWhiteSpace(testing.InstalledVersion)
                ? "missing"
                : testing.InstalledVersion!;
            throw new InvalidOperationException(
                $"@webstir/test {installed} detected but {testing.Metadata.Version} is bundled. Run '{App.Name} install' to refresh dependencies.");
        }
    }
}
