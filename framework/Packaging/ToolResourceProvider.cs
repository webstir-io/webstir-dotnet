namespace Framework.Packaging;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

internal static class ToolResourceProvider
{
    private const string ResourcePrefix = "Framework.Resources.tools.";

    internal static async Task CopyEmbeddedToolsAsync(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        Directory.CreateDirectory(destinationPath);
        foreach (string resourceName in typeof(ToolResourceProvider).Assembly.GetManifestResourceNames().Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            await using Stream? resourceStream = typeof(ToolResourceProvider).Assembly.GetManifestResourceStream(resourceName);
            if (resourceStream is null)
            {
                continue;
            }

            string relativePath = resourceName[ResourcePrefix.Length..];
            string outputPath = Path.Combine(destinationPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            await using FileStream fileStream = File.Create(outputPath);
            await resourceStream.CopyToAsync(fileStream);
        }
    }

    internal static async Task<ToolPackageManifest> LoadManifestAsync(string manifestFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestFileName);

        string resourceName = ResourcePrefix + manifestFileName;
        await using Stream? stream = typeof(ToolResourceProvider).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Unable to load tool manifest: {manifestFileName}");
        }

        using JsonDocument document = await JsonDocument.ParseAsync(stream);
        JsonElement root = document.RootElement;

        string name = root.GetProperty("name").GetString() ?? throw new InvalidOperationException("Manifest missing package name.");
        string version = root.GetProperty("version").GetString() ?? throw new InvalidOperationException("Manifest missing package version.");
        string fileName = root.GetProperty("fileName").GetString() ?? throw new InvalidOperationException("Manifest missing fileName.");
        string dependency = root.GetProperty("dependency").GetString() ?? throw new InvalidOperationException("Manifest missing dependency string.");
        string? hash = root.TryGetProperty("hash", out JsonElement hashElement) ? hashElement.GetString() : null;
        string? registrySpecifier = root.TryGetProperty("registrySpecifier", out JsonElement registryElement) ? registryElement.GetString() : null;

        return new ToolPackageManifest(name, version, fileName, dependency, hash, registrySpecifier);
    }
}

internal readonly record struct ToolPackageManifest(
    string Name,
    string Version,
    string FileName,
    string Dependency,
    string? Hash,
    string? RegistrySpecifier);
