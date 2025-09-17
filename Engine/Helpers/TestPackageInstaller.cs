using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Engine.Extensions;

namespace Engine.Helpers;

internal static class TestPackageInstaller
{
    private const string ManifestFileName = "testing-package.json";

    private static readonly Lazy<TestPackageMetadata> Manifest = new(LoadManifest, isThreadSafe: true);

    internal static async Task<PackageEnsureResult> EnsureAsync(AppWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        TestPackageMetadata metadata = Manifest.Value;
        string toolsDirectory = workspace.WorkingPath.Combine(Folders.Tools);
        string tarballPath = toolsDirectory.Combine(metadata.FileName);
        bool hadTarball = File.Exists(tarballPath);

        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.ToolsPath, toolsDirectory);
        bool hasTarball = File.Exists(tarballPath);

        bool dependencyUpdated = await EnsureDependencyAsync(workspace.WorkingPath.Combine(Files.PackageJson), metadata);
        bool tarballUpdated = await DetectTarballMismatchAsync(tarballPath, metadata.Hash);
        PackageInstallState installState = await DetectInstalledVersionMismatchAsync(workspace, metadata);

        return new PackageEnsureResult(!hadTarball && hasTarball, dependencyUpdated, tarballUpdated, installState.VersionMismatch, installState.InstalledVersion, metadata);
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

        return new TestPackageMetadata(name, version, fileName, dependency, hash);
    }

    private static async Task<bool> EnsureDependencyAsync(string packageJsonPath, TestPackageMetadata metadata)
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
            if (string.Equals(currentValue, metadata.Dependency, StringComparison.Ordinal))
            {
                return false;
            }

            dependencies[metadata.Name] = metadata.Dependency;

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
            Console.Error.WriteLine($"Warning: Unable to read installed @webstir/test version: {ex.Message}");
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
}

internal readonly record struct PackageEnsureResult(
    bool ToolsAdded,
    bool DependencyUpdated,
    bool TarballUpdated,
    bool VersionMismatch,
    string? InstalledVersion,
    TestPackageMetadata Metadata);

internal readonly record struct PackageInstallState(bool VersionMismatch, string? InstalledVersion);

internal readonly record struct TestPackageMetadata(
    string Name,
    string Version,
    string FileName,
    string Dependency,
    string? Hash)
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
