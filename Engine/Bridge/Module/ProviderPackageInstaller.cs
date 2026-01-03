using System;
using System.IO;
using System.Threading.Tasks;
using Framework.Packaging;

namespace Engine.Bridge.Module;

internal static class ProviderPackageInstaller
{
    internal static async Task EnsureAsync(
        IPackageWorkspace workspace,
        string providerId,
        string? providerSpec,
        string defaultProviderId,
        string label,
        Action<string> log)
    {
        if (string.IsNullOrWhiteSpace(providerId) ||
            string.Equals(providerId, defaultProviderId, StringComparison.Ordinal) ||
            LooksLikePath(providerId))
        {
            return;
        }

        if (IsPackagePresent(workspace, providerId))
        {
            return;
        }

        string installSpec = string.IsNullOrWhiteSpace(providerSpec) ? providerId : providerSpec;
        log($"[packages] Installing {label} provider override '{installSpec}'.");
        await workspace.InstallPackagesAsync(new[] { installSpec }).ConfigureAwait(false);
    }

    private static bool LooksLikePath(string providerId) =>
        providerId.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
        providerId.StartsWith(".", StringComparison.Ordinal) ||
        Path.IsPathRooted(providerId);

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
