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
        string packageJsonPath = Path.Combine(workspace.WorkingPath, "package.json");

        string dependencySpecifier = await ResolveDependencySpecifierAsync(workspace, metadata, preferRegistry);

        bool dependencyUpdated = await EnsureDependencyAsync(packageJsonPath, metadata, dependencySpecifier);
        PackageInstallState installState = await DetectInstalledVersionMismatchAsync(workspace, metadata);

        return new PackageEnsureResult(dependencyUpdated, installState.VersionMismatch, installState.InstalledVersion, metadata);
    }

    private static async Task<string> ResolveDependencySpecifierAsync(IPackageWorkspace workspace, FrameworkPackageMetadata metadata, bool preferRegistry)
    {
        if (!preferRegistry)
        {
            try
            {
                await PackageTarballManager.EnsureTarballAsync(workspace, metadata);
                string specifier = metadata.GetWorkspaceDependencySpecifier();
                WriteTestingPackageManifest(workspace, metadata, specifier);
                return specifier;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Falling back to registry for {metadata.Name}: {ex.Message}");
            }
        }

        DeleteTestingPackageManifest(workspace);
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

    private static void WriteTestingPackageManifest(IPackageWorkspace workspace, FrameworkPackageMetadata metadata, string dependencySpecifier)
    {
        try
        {
            Directory.CreateDirectory(workspace.WebstirPath);
            string manifestPath = Path.Combine(workspace.WebstirPath, "testing-package.json");

            JsonObject manifest = new()
            {
                ["fileName"] = metadata.Tarball.FileName,
                ["dependency"] = dependencySpecifier
            };

            JsonSerializerOptions options = new()
            {
                WriteIndented = true
            };

            File.WriteAllText(manifestPath, manifest.ToJsonString(options) + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Warning: Unable to write testing-package.json: {ex.Message}");
        }
    }

    private static void DeleteTestingPackageManifest(IPackageWorkspace workspace)
    {
        try
        {
            string manifestPath = Path.Combine(workspace.WebstirPath, "testing-package.json");
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Warning: Unable to delete testing-package.json: {ex.Message}");
        }
    }
}

public readonly record struct PackageEnsureResult(
    bool DependencyUpdated,
    bool VersionMismatch,
    string? InstalledVersion,
    FrameworkPackageMetadata Metadata) : IPackageEnsureResult;

internal readonly record struct PackageInstallState(bool VersionMismatch, string? InstalledVersion);
