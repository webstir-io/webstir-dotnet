namespace Framework.Packaging;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public static class FrontendPackageInstaller
{
    public static async Task<FrontendPackageEnsureResult> EnsureAsync(IPackageWorkspace workspace, bool preferRegistry)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        FrameworkPackageMetadata metadata = FrameworkPackageCatalog.Frontend;
        string packageJsonPath = Path.Combine(workspace.WorkingPath, "package.json");

        string dependencySpecifier = await ResolveDependencySpecifierAsync(workspace, metadata, preferRegistry);

        bool dependencyUpdated = await EnsureDependencyAsync(packageJsonPath, metadata, dependencySpecifier);
        FrontendPackageInstallState installState = await DetectInstalledVersionMismatchAsync(workspace, metadata);

        return new FrontendPackageEnsureResult(dependencyUpdated, installState.VersionMismatch, installState.InstalledVersion, metadata);
    }

    private static async Task<string> ResolveDependencySpecifierAsync(IPackageWorkspace workspace, FrameworkPackageMetadata metadata, bool preferRegistry)
    {
        if (!preferRegistry)
        {
            try
            {
                await PackageTarballManager.EnsureTarballAsync(workspace, metadata);
                return metadata.GetWorkspaceDependencySpecifier();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Falling back to registry for {metadata.Name}: {ex.Message}");
            }
        }

        return metadata.RegistrySpecifier;
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

            if (obj["devDependencies"] is JsonObject devDependencies && devDependencies.ContainsKey(metadata.Name))
            {
                devDependencies.Remove(metadata.Name);
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

    private static async Task<FrontendPackageInstallState> DetectInstalledVersionMismatchAsync(IPackageWorkspace workspace, FrameworkPackageMetadata metadata)
    {
        string packageJsonPath = metadata.GetInstalledPackageJsonPath(workspace);
        if (!File.Exists(packageJsonPath))
        {
            return new FrontendPackageInstallState(true, null);
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(await File.ReadAllTextAsync(packageJsonPath));
            string installedVersion = doc.RootElement.GetProperty("version").GetString() ?? string.Empty;
            bool mismatch = !string.Equals(installedVersion, metadata.Version, StringComparison.Ordinal);
            return new FrontendPackageInstallState(mismatch, installedVersion);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Unable to read installed {metadata.Name} version: {ex.Message}");
            return new FrontendPackageInstallState(true, null);
        }
    }
}

public readonly record struct FrontendPackageEnsureResult(
    bool DependencyUpdated,
    bool VersionMismatch,
    string? InstalledVersion,
    FrameworkPackageMetadata Metadata) : IPackageEnsureResult;

internal readonly record struct FrontendPackageInstallState(bool VersionMismatch, string? InstalledVersion);
