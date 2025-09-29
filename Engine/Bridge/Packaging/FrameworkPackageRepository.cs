using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Engine.Bridge.Packaging;

internal static class FrameworkPackageRepository
{
    private const string ManifestFileName = "manifest.json";
    private const string PackageRootEnvironmentVariable = "WEBSTIR_PACKAGE_ROOT";

    private sealed record RepositoryState(string? ManifestPath, Dictionary<string, FrameworkPackageManifestEntry>? Manifest);

    private static readonly Lazy<RepositoryState> State = new(Initialize, isThreadSafe: true);

    internal static bool TryGetPackage(string packageName, out FrameworkPackageManifestEntry entry)
    {
        RepositoryState state = State.Value;
        if (state.Manifest is null)
        {
            entry = default;
            return false;
        }

        return state.Manifest.TryGetValue(packageName, out entry);
    }

    private static RepositoryState Initialize()
    {
        string? manifestPath = LocateManifest();
        if (string.IsNullOrEmpty(manifestPath))
        {
            return new RepositoryState(null, null);
        }

        try
        {
            string json = File.ReadAllText(manifestPath);
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("packages", out JsonElement packagesElement) || packagesElement.ValueKind != JsonValueKind.Object)
            {
                return new RepositoryState(manifestPath, null);
            }

            Dictionary<string, FrameworkPackageManifestEntry> packages = new(StringComparer.Ordinal);
            string manifestDirectory = Path.GetDirectoryName(manifestPath)!;

            foreach (JsonProperty property in packagesElement.EnumerateObject())
            {
                JsonElement packageElement = property.Value;

                string name = property.Name;
                string version = packageElement.TryGetProperty("version", out JsonElement versionElement) ? versionElement.GetString() ?? string.Empty : string.Empty;
                string fileName = packageElement.TryGetProperty("fileName", out JsonElement fileNameElement) ? fileNameElement.GetString() ?? string.Empty : string.Empty;
                string? dependency = packageElement.TryGetProperty("dependency", out JsonElement dependencyElement) ? dependencyElement.GetString() : null;
                string? hash = packageElement.TryGetProperty("hash", out JsonElement hashElement) ? hashElement.GetString() : null;
                if (!packageElement.TryGetProperty("repositoryPath", out JsonElement repositoryPathElement))
                {
                    continue;
                }

                string? repositoryPathValue = repositoryPathElement.GetString();
                if (string.IsNullOrWhiteSpace(repositoryPathValue))
                {
                    continue;
                }

                string normalizedRelativePath = repositoryPathValue.Replace('/', Path.DirectorySeparatorChar);
                string absolutePath = Path.GetFullPath(Path.Combine(manifestDirectory, normalizedRelativePath));

                packages[name] = new FrameworkPackageManifestEntry(name, version, fileName, dependency, hash, repositoryPathValue, absolutePath);
            }

            return new RepositoryState(manifestPath, packages);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: unable to read framework package manifest at {manifestPath}: {ex.Message}");
            return new RepositoryState(manifestPath, null);
        }
    }

    private static string? LocateManifest()
    {
        string? configuredRoot = Environment.GetEnvironmentVariable(PackageRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            string candidate = Directory.Exists(configuredRoot)
                ? Path.Combine(configuredRoot, ManifestFileName)
                : configuredRoot;

            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            string candidate = Path.Combine(current, ManifestFileName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }

            string frameworkRelative = Path.Combine(current, "framework", "out", ManifestFileName);
            if (File.Exists(frameworkRelative))
            {
                return Path.GetFullPath(frameworkRelative);
            }

            string? parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent;
        }

        return null;
    }
}

internal readonly record struct FrameworkPackageManifestEntry(
    string Name,
    string Version,
    string FileName,
    string? Dependency,
    string? Hash,
    string RepositoryPath,
    string AbsolutePath);
