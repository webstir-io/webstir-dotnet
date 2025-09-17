using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Engine.Extensions;

namespace Engine.Helpers;

internal static class FrontendPackageInstaller
{
    private const string ManifestFileName = "frontend-package.json";

    private static readonly Lazy<FrontendPackageMetadata> Manifest = new(LoadManifest, isThreadSafe: true);

    internal static async Task<FrontendPackageEnsureResult> EnsureAsync(AppWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        FrontendPackageMetadata metadata = Manifest.Value;
        string toolsDirectory = workspace.WorkingPath.Combine(Folders.Tools);
        string tarballPath = toolsDirectory.Combine(metadata.FileName);
        bool hadTarball = File.Exists(tarballPath);

        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.ToolsPath, toolsDirectory);
        bool hasTarball = File.Exists(tarballPath);

        bool dependencyUpdated = await EnsureDependencyAsync(workspace.WorkingPath.Combine(Files.PackageJson), metadata);
        FrontendPackageInstallState installState = await DetectInstalledVersionMismatchAsync(workspace, metadata);

        return new FrontendPackageEnsureResult(!hadTarball && hasTarball, dependencyUpdated, installState.VersionMismatch, installState.InstalledVersion, metadata);
    }

    private static FrontendPackageMetadata LoadManifest()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"{Resources.ToolsPath}.{ManifestFileName}";

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Unable to load frontend package manifest: {resourceName}");
        }

        using JsonDocument doc = JsonDocument.Parse(stream);
        JsonElement root = doc.RootElement;

        string name = root.GetProperty("name").GetString() ?? throw new InvalidOperationException("Manifest missing package name.");
        string version = root.GetProperty("version").GetString() ?? throw new InvalidOperationException("Manifest missing package version.");
        string fileName = root.GetProperty("fileName").GetString() ?? throw new InvalidOperationException("Manifest missing fileName.");
        string dependency = root.GetProperty("dependency").GetString() ?? throw new InvalidOperationException("Manifest missing dependency string.");

        return new FrontendPackageMetadata(name, version, fileName, dependency);
    }

    private static async Task<bool> EnsureDependencyAsync(string packageJsonPath, FrontendPackageMetadata metadata)
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

    private static async Task<FrontendPackageInstallState> DetectInstalledVersionMismatchAsync(AppWorkspace workspace, FrontendPackageMetadata metadata)
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
            Console.Error.WriteLine($"Warning: Unable to read installed @webstir/frontend version: {ex.Message}");
            return new FrontendPackageInstallState(true, null);
        }
    }
}

internal readonly record struct FrontendPackageEnsureResult(
    bool ToolsAdded,
    bool DependencyUpdated,
    bool VersionMismatch,
    string? InstalledVersion,
    FrontendPackageMetadata Metadata);

internal readonly record struct FrontendPackageInstallState(bool VersionMismatch, string? InstalledVersion);

internal readonly record struct FrontendPackageMetadata(
    string Name,
    string Version,
    string FileName,
    string Dependency)
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
