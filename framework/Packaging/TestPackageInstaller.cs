using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Framework.Packaging;

public static class TestPackageInstaller
{
    public static async Task<PackageEnsureResult> EnsureAsync(IPackageWorkspace workspace, bool preferRegistry)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        FrameworkPackageMetadata metadata = FrameworkPackageCatalog.Testing;
        _ = preferRegistry; // Registry usage is mandatory in the registry-first flow.
        string packageJsonPath = Path.Combine(workspace.WorkingPath, "package.json");

        bool dependencyUpdated = await EnsureDependencyAsync(packageJsonPath, metadata);
        PackageInstallState installState = await DetectInstalledVersionMismatchAsync(workspace, metadata);

        return new PackageEnsureResult(dependencyUpdated, installState.VersionMismatch, installState.InstalledVersion, metadata);
    }

    private static async Task<bool> EnsureDependencyAsync(string packageJsonPath, FrameworkPackageMetadata metadata)
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

            // Use a plain version specifier in dependencies to avoid npm alias/link indirection
            string desiredSpecifier = metadata.Version;
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

    private static async Task<PackageInstallState> DetectInstalledVersionMismatchAsync(IPackageWorkspace workspace, FrameworkPackageMetadata metadata)
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
            bool mismatch = !string.Equals(installedVersion, metadata.Version, StringComparison.Ordinal);
            return new PackageInstallState(mismatch, installedVersion);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Unable to read installed {metadata.Name} version: {ex.Message}");
            return new PackageInstallState(true, null);
        }
    }
}

public readonly record struct PackageEnsureResult(
    bool DependencyUpdated,
    bool VersionMismatch,
    string? InstalledVersion,
    FrameworkPackageMetadata Metadata) : IPackageEnsureResult;

internal readonly record struct PackageInstallState(bool VersionMismatch, string? InstalledVersion);
