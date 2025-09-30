namespace Framework.Packaging;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

internal static class FrameworkPackageCatalog
{
    private const string ResourceName = "Framework.Packaging.framework-packages.json";

    private static readonly Lazy<IDictionary<string, FrameworkPackageMetadata>> Packages = new(Load, true);

    internal static FrameworkPackageMetadata Frontend => Get("@electric-coding-llc/webstir-frontend");

    internal static FrameworkPackageMetadata Testing => Get("@electric-coding-llc/webstir-test");

    internal static FrameworkPackageMetadata Get(string packageName)
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
            result[property.Name] = new FrameworkPackageMetadata(name, version, registrySpecifier);
        }

        return result;
    }
}

public readonly record struct FrameworkPackageMetadata(string Name, string Version, string RegistrySpecifier)
{
    internal string VersionSafe => Version.Replace('.', '-');

    internal string GetInstalledPackageJsonPath(IPackageWorkspace workspace)
    {
        string path = workspace.NodeModulesPath;
        foreach (string segment in Name.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            path = Path.Combine(path, segment);
        }

        return Path.Combine(path, "package.json");
    }
}
