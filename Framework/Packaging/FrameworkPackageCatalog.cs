using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Framework.Packaging;

public static class FrameworkPackageCatalog
{
    private const string ResourceName = "Framework.Packaging.framework-packages.json";

    private static readonly Lazy<IDictionary<string, FrameworkPackageMetadata>> Packages = new(Load, true);

    public static FrameworkPackageMetadata Frontend => Get("@webstir-io/webstir-frontend");

    public static FrameworkPackageMetadata Testing => Get("@webstir-io/webstir-test");

    public static FrameworkPackageMetadata Backend => Get("@webstir-io/webstir-backend");

    public static FrameworkPackageMetadata Get(string packageName)
    {
        if (!Packages.Value.TryGetValue(packageName, out FrameworkPackageMetadata metadata))
        {
            throw new InvalidOperationException($"Framework package metadata missing for '{packageName}'.");
        }

        return metadata;
    }

    private static IDictionary<string, FrameworkPackageMetadata> Load()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream? stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Unable to load framework package catalog resource '{ResourceName}'.");
        }

        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("packages", out JsonElement packagesElement) || packagesElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Framework package catalog missing 'packages' node.");
        }

        Dictionary<string, FrameworkPackageMetadata> result = new(StringComparer.Ordinal);
        foreach (JsonProperty property in packagesElement.EnumerateObject())
        {
            JsonElement value = property.Value;
            string name = value.GetProperty("name").GetString() ?? property.Name;
            string version = value.GetProperty("version").GetString() ?? throw new InvalidOperationException($"Package '{property.Name}' missing version metadata.");
            string registrySpecifier = value.GetProperty("registrySpecifier").GetString() ?? throw new InvalidOperationException($"Package '{property.Name}' missing registry specifier metadata.");
            FrameworkPackageTarballMetadata tarball = ParseTarballMetadata(property.Name, value);
            result[property.Name] = new FrameworkPackageMetadata(name, version, registrySpecifier, tarball);
        }

        return result;
    }

    private static FrameworkPackageTarballMetadata ParseTarballMetadata(string packageName, JsonElement value)
    {
        if (!value.TryGetProperty("tarball", out JsonElement tarballElement) || tarballElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Package '{packageName}' missing tarball metadata.");
        }

        string fileName = tarballElement.GetProperty("fileName").GetString()
            ?? throw new InvalidOperationException($"Package '{packageName}' tarball missing fileName.");
        string repositoryPath = tarballElement.GetProperty("repositoryPath").GetString()
            ?? throw new InvalidOperationException($"Package '{packageName}' tarball missing repositoryPath.");
        string sha256 = tarballElement.GetProperty("sha256").GetString()
            ?? throw new InvalidOperationException($"Package '{packageName}' tarball missing sha256 hash.");
        long size = tarballElement.GetProperty("size").GetInt64();

        return new FrameworkPackageTarballMetadata(fileName, repositoryPath, sha256, size);
    }
}

public readonly record struct FrameworkPackageMetadata(
    string Name,
    string Version,
    string RegistrySpecifier,
    FrameworkPackageTarballMetadata Tarball)
{
    internal string VersionSafe => Version.Replace('.', '-');

    internal string GetInstalledPackageJsonPath(IPackageWorkspace workspace)
    {
        string path = workspace.NodeModulesPath;
        foreach (string segment in Name.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            path = Path.Combine(path, segment);
        }

        string packageJson = Path.Combine(path, "package.json");
        if (File.Exists(packageJson))
        {
            return packageJson;
        }

        string? resolved = ResolvePackageJsonPath(path);
        return resolved ?? packageJson;
    }

    private static string? ResolvePackageJsonPath(string expectedDirectory)
    {
        string? resolved = ResolveViaSymlink(expectedDirectory);
        if (!string.IsNullOrEmpty(resolved))
        {
            string candidate = Path.Combine(resolved, "package.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string? parent = Path.GetDirectoryName(expectedDirectory);
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
        {
            return null;
        }

        string packageName = Path.GetFileName(expectedDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(packageName))
        {
            return null;
        }

        // npm 9/10+ may materialize packages as <name>@<version> under the scope directory.
        string? versionedDirectory = Directory.EnumerateDirectories(parent, packageName + "@*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(directory => File.Exists(Path.Combine(directory, "package.json")));
        if (!string.IsNullOrEmpty(versionedDirectory))
        {
            return Path.Combine(versionedDirectory, "package.json");
        }

        string hiddenPattern = $".{packageName}-*";
        string? hiddenDirectory = Directory.EnumerateDirectories(parent, hiddenPattern, SearchOption.TopDirectoryOnly)
            .FirstOrDefault(directory => File.Exists(Path.Combine(directory, "package.json")));
        if (!string.IsNullOrEmpty(hiddenDirectory))
        {
            return Path.Combine(hiddenDirectory, "package.json");
        }

        // npm 10+ can create a nested node_modules folder under the scope directory.
        string nested = Path.Combine(parent, "node_modules", packageName, "package.json");
        if (File.Exists(nested))
        {
            return nested;
        }

        return null;
    }

    private static string? ResolveViaSymlink(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                DirectoryInfo info = new(directoryPath);
                FileSystemInfo? target = info.ResolveLinkTarget(returnFinalTarget: true);
                return (target as DirectoryInfo)?.FullName;
            }
        }
        catch (IOException)
        {
            // ignore and fall back to other resolution strategies
        }
        catch (UnauthorizedAccessException)
        {
            // ignore and fall back to other resolution strategies
        }
        catch (PlatformNotSupportedException)
        {
            // ignore
        }

        return null;
    }

    internal Stream OpenTarballStream() => Tarball.OpenStream();

    internal string GetWorkspaceTarballPath(IPackageWorkspace workspace)
    {
        string directory = workspace.WebstirPath;
        return Path.Combine(directory, Tarball.FileName);
    }

    internal string GetWorkspaceDependencySpecifier() => Tarball.WorkspaceSpecifier;
}
