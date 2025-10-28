using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Engine.Bridge.Module;

namespace Engine.Bridge.Backend;

internal static class BackendManifestLoader
{
    private static readonly JsonSerializerOptions ManifestSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static async Task<ModuleBuildManifest> LoadAsync(AppWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return await LoadAsync(workspace.BackendManifestPath, cancellationToken);
    }

    public static async Task<ModuleBuildManifest> LoadAsync(string manifestPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifestPath);

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Backend manifest not found at {manifestPath}.", manifestPath);
        }

        await using FileStream stream = File.OpenRead(manifestPath);
        ModuleBuildManifest? manifest = await JsonSerializer.DeserializeAsync<ModuleBuildManifest>(
            stream,
            ManifestSerializerOptions,
            cancellationToken);

        if (manifest is null)
        {
            throw new InvalidOperationException($"Failed to deserialize backend manifest at {manifestPath}.");
        }

        return manifest;
    }
}
