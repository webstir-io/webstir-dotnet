using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Framework.Packaging;

public static class BackendPackageInstaller
{
    public static async Task<PackageEnsureResult> EnsureAsync(
        IPackageWorkspace workspace,
        string? dependencySpecifierOverride = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        FrameworkPackageMetadata metadata = FrameworkPackageCatalog.Backend;
        string packageJsonPath = Path.Combine(workspace.WorkingPath, "package.json");

        string dependencySpecifier = string.IsNullOrWhiteSpace(dependencySpecifierOverride)
            ? metadata.WorkspaceSpecifier
            : dependencySpecifierOverride;

        bool dependencyUpdated = await EnsureDependencyAsync(packageJsonPath, metadata, dependencySpecifier);
        bool ignoreVersionMismatch = !string.IsNullOrWhiteSpace(dependencySpecifierOverride);
        PackageInstallState installState = await DetectInstalledVersionMismatchAsync(workspace, metadata, ignoreVersionMismatch);

        return new PackageEnsureResult(dependencyUpdated, installState.VersionMismatch, installState.InstalledVersion, metadata);
    }

    private static async Task<bool> EnsureDependencyAsync(string packageJsonPath, FrameworkPackageMetadata metadata, string desiredSpecifier)
    {
        if (!File.Exists(packageJsonPath))
        {
            return false;
        }

        try
        {
            string json = await File.ReadAllTextAsync(packageJsonPath);
            JsonNode? root = JsonNode.Parse(json);
            if (root is not JsonObject obj)
            {
                return false;
            }

            if (obj["dependencies"] is not JsonObject dependencies)
            {
                dependencies = [];
                obj["dependencies"] = dependencies;
            }

            string? currentValue = dependencies[metadata.Name]?.GetValue<string>();
            if (string.Equals(currentValue, desiredSpecifier, StringComparison.Ordinal))
            {
                return false;
            }

            dependencies[metadata.Name] = desiredSpecifier;

            JsonSerializerOptions options = new()
            {
                WriteIndented = true
            };

            await File.WriteAllTextAsync(packageJsonPath, obj.ToJsonString(options));
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Unable to update package.json: {ex.Message}");
            return false;
        }
    }

    private static async Task<PackageInstallState> DetectInstalledVersionMismatchAsync(
        IPackageWorkspace workspace,
        FrameworkPackageMetadata metadata,
        bool ignoreVersionMismatch)
    {
        string packageJsonPath = metadata.GetInstalledPackageJsonPath(workspace);
        if (!File.Exists(packageJsonPath))
        {
            return new PackageInstallState(true, null);
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(await File.ReadAllTextAsync(packageJsonPath));
            string installedVersion = doc.RootElement.GetProperty("version").GetString() ?? string.Empty;
            bool mismatch = !ignoreVersionMismatch &&
                !string.Equals(installedVersion, metadata.Version, StringComparison.Ordinal);
            return new PackageInstallState(mismatch, installedVersion);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Unable to read installed {metadata.Name} version: {ex.Message}");
            return new PackageInstallState(true, null);
        }
    }
}
