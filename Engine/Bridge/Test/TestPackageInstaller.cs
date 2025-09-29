using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Engine.Bridge.Packaging;
using Engine.Extensions;
using Engine.Helpers;

namespace Engine.Bridge.Test;

internal static class TestPackageInstaller
{
    private const string ManifestFileName = "testing-package.json";

    private static readonly Lazy<TestPackageMetadata> Manifest = new(LoadManifest, isThreadSafe: true);

    internal static async Task<PackageEnsureResult> EnsureAsync(AppWorkspace workspace, bool preferRegistry)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        TestPackageMetadata metadata = Manifest.Value;
        string toolsDirectory = workspace.WorkingPath.Combine(Folders.Tools);
        Directory.CreateDirectory(toolsDirectory);

        string tarballPath = toolsDirectory.Combine(metadata.FileName);
        bool hadTarball = File.Exists(tarballPath);

        bool copiedFromRepository = await TryCopyFromRepositoryAsync(metadata, tarballPath);
        if (!copiedFromRepository)
        {
            await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.ToolsPath, toolsDirectory);
        }

        bool hasTarball = File.Exists(tarballPath);

        await WriteManifestAsync(Path.Combine(toolsDirectory, ManifestFileName), metadata);

        bool dependencyUpdated = await EnsureDependencyAsync(workspace.WorkingPath.Combine(Files.PackageJson), metadata, preferRegistry);
        bool tarballUpdated = await DetectTarballMismatchAsync(tarballPath, metadata.Hash);
        PackageInstallState installState = await DetectInstalledVersionMismatchAsync(workspace, metadata);

        return new PackageEnsureResult(!hadTarball && hasTarball, dependencyUpdated, tarballUpdated, installState.VersionMismatch, installState.InstalledVersion, metadata);
    }

    private static async Task<bool> TryCopyFromRepositoryAsync(TestPackageMetadata metadata, string destinationTarballPath)
    {
        if (!FrameworkPackageRepository.TryGetPackage(metadata.Name, out FrameworkPackageManifestEntry entry))
        {
            return false;
        }

        if (!string.Equals(entry.Version, metadata.Version, StringComparison.Ordinal) || !string.Equals(entry.FileName, metadata.FileName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!File.Exists(entry.AbsolutePath))
        {
            Console.Error.WriteLine($"Warning: Local package archive not found for {metadata.Name} at {entry.AbsolutePath}.");
            return false;
        }

        if (!string.IsNullOrEmpty(metadata.Hash))
        {
            string repositoryHash = await ComputeFileHashAsync(entry.AbsolutePath);
            if (!string.Equals(repositoryHash, metadata.Hash, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Warning: Local package archive hash mismatch for {metadata.Name}; falling back to embedded resources.");
                return false;
            }
        }

        try
        {
            File.Copy(entry.AbsolutePath, destinationTarballPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Unable to copy {metadata.Name} from local repository: {ex.Message}");
            return false;
        }
    }

    private static async Task WriteManifestAsync(string manifestPath, TestPackageMetadata metadata)
    {
        JsonObject obj = new()
        {
            ["name"] = metadata.Name,
            ["version"] = metadata.Version,
            ["fileName"] = metadata.FileName,
            ["dependency"] = metadata.Dependency
        };

        if (!string.IsNullOrEmpty(metadata.Hash))
        {
            obj["hash"] = metadata.Hash;
        }

        if (!string.IsNullOrEmpty(metadata.RegistrySpecifier))
        {
            obj["registrySpecifier"] = metadata.RegistrySpecifier;
        }

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        await File.WriteAllTextAsync(manifestPath, obj.ToJsonString(options));
    }

    private static TestPackageMetadata LoadManifest()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"{Resources.ToolsPath}.{ManifestFileName}";

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Unable to load test package manifest: {resourceName}");
        }

        using JsonDocument doc = JsonDocument.Parse(stream);
        JsonElement root = doc.RootElement;

        string name = root.GetProperty("name").GetString() ?? throw new InvalidOperationException("Manifest missing package name.");
        string version = root.GetProperty("version").GetString() ?? throw new InvalidOperationException("Manifest missing package version.");
        string fileName = root.GetProperty("fileName").GetString() ?? throw new InvalidOperationException("Manifest missing fileName.");
        string dependency = root.GetProperty("dependency").GetString() ?? throw new InvalidOperationException("Manifest missing dependency string.");
        string? hash = root.TryGetProperty("hash", out JsonElement hashElement) ? hashElement.GetString() : null;
        string? registrySpecifier = root.TryGetProperty("registrySpecifier", out JsonElement registryElement) ? registryElement.GetString() : null;

        return new TestPackageMetadata(name, version, fileName, dependency, hash, registrySpecifier);
    }

    private static async Task<bool> EnsureDependencyAsync(string packageJsonPath, TestPackageMetadata metadata, bool preferRegistry)
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

            string desiredSpecifier = GetDependencySpecifier(metadata, preferRegistry);

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
            Console.Error.WriteLine($"Warning: Unable to update {Files.PackageJson}: {ex.Message}");
            return false;
        }
    }

    private static async Task<PackageInstallState> DetectInstalledVersionMismatchAsync(AppWorkspace workspace, TestPackageMetadata metadata)
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
            Console.Error.WriteLine($"Warning: Unable to read installed @electric-coding-llc/webstir-test version: {ex.Message}");
            return new PackageInstallState(true, null);
        }
    }

    private static async Task<bool> DetectTarballMismatchAsync(string tarballPath, string? expectedHash)
    {
        if (string.IsNullOrEmpty(expectedHash))
        {
            return false;
        }

        if (!File.Exists(tarballPath))
        {
            return true;
        }

        string actualHash = await ComputeFileHashAsync(tarballPath);
        return !string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ComputeFileHashAsync(string filePath)
    {
        await using FileStream stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetDependencySpecifier(TestPackageMetadata metadata, bool preferRegistry)
    {
        if (preferRegistry && !string.IsNullOrWhiteSpace(metadata.RegistrySpecifier))
        {
            return metadata.RegistrySpecifier!;
        }

        return metadata.Dependency;
    }
}

internal readonly record struct PackageEnsureResult(
    bool ToolsAdded,
    bool DependencyUpdated,
    bool TarballUpdated,
    bool VersionMismatch,
    string? InstalledVersion,
    TestPackageMetadata Metadata) : IPackageEnsureResult;

internal readonly record struct PackageInstallState(bool VersionMismatch, string? InstalledVersion);

internal readonly record struct TestPackageMetadata(
    string Name,
    string Version,
    string FileName,
    string Dependency,
    string? Hash,
    string? RegistrySpecifier)
{
    private const char ScopeSeparator = '/';

    internal string GetInstalledPackageJsonPath(AppWorkspace workspace)
    {
        string path = workspace.WorkingPath.Combine(Folders.NodeModules);
        foreach (string segment in Name.Split(ScopeSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            path = path.Combine(segment);
        }

        return path.Combine(Files.PackageJson);
    }
}
