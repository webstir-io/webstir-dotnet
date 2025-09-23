using System;
using System.IO;
using System.Threading.Tasks;

using Engine;
using Engine.Bridge;

namespace Engine.Bridge.Test;

internal static class TestPackageUtilities
{
    internal static async Task<PackageEnsureResult> EnsurePackageAsync(AppWorkspace workspace)
    {
        NodeRuntime.EnsureMinimumVersion();
        PackageEnsureResult result = await TestPackageInstaller.EnsureAsync(workspace);

        if (result.ToolsAdded || result.DependencyUpdated || result.TarballUpdated)
        {
            NpmHelper.RunNpmInstall(workspace.WorkingPath);
            result = await TestPackageInstaller.EnsureAsync(workspace);
        }

        return result;
    }

    internal static void LogEnsureMessages(PackageEnsureResult result)
    {
        if (result.ToolsAdded)
        {
            Console.WriteLine($"Added testing package archive: {Path.Combine(Folders.Tools, result.Metadata.FileName)}");
        }

        if (result.DependencyUpdated)
        {
            Console.WriteLine($"Pinned @webstir/test to {result.Metadata.Dependency} in {Files.PackageJson}");
        }

        if (result.TarballUpdated)
        {
            Console.WriteLine("Updated @webstir/test tarball; npm install rerun may be required.");
        }

        if (result.VersionMismatch)
        {
            string installed = string.IsNullOrWhiteSpace(result.InstalledVersion)
                ? "not installed"
                : result.InstalledVersion!;
            Console.WriteLine($"Warning: @webstir/test {installed} differs from packaged {result.Metadata.Version}. Run 'npm install' to refresh node_modules.");
        }
    }
}
