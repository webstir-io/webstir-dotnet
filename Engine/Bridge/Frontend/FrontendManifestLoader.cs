using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Engine.Bridge.Frontend;

public static class FrontendManifestLoader
{
    private static readonly JsonSerializerOptions ManifestSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static async Task<FrontendManifest> LoadAsync(AppWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return await LoadAsync(workspace.FrontendManifestPath, cancellationToken);
    }

    public static async Task<FrontendManifest> LoadAsync(string manifestPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifestPath);

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                $"Frontend manifest not found at {manifestPath}. Run the frontend CLI to generate it.",
                manifestPath);
        }

        await using FileStream stream = File.OpenRead(manifestPath);
        FrontendManifest? manifest = await JsonSerializer.DeserializeAsync<FrontendManifest>(stream, ManifestSerializerOptions, cancellationToken) ?? throw new InvalidOperationException($"Failed to deserialize frontend manifest at {manifestPath}.");

        Validate(manifest, manifestPath);
        return manifest;
    }

    private static void Validate(FrontendManifest manifest, string manifestPath)
    {
        if (manifest.Version != 1)
        {
            throw new InvalidOperationException(
                $"Unsupported frontend manifest version '{manifest.Version}' at {manifestPath}. Update the CLI or regenerate the manifest.");
        }

        ValidatePath(manifest.Paths.Workspace, "paths.workspace", manifestPath);
        ValidatePathGroup(manifest.Paths.Src, "paths.src", manifestPath);
        ValidatePathGroup(manifest.Paths.Build, "paths.build", manifestPath);
        ValidatePathGroup(manifest.Paths.Dist, "paths.dist", manifestPath);
    }

    private static void ValidatePathGroup(FrontendManifestPathGroup group, string property, string manifestPath)
    {
        ArgumentNullException.ThrowIfNull(group);

        ValidatePath(group.Root, $"{property}.root", manifestPath);
        ValidatePath(group.Frontend, $"{property}.frontend", manifestPath);
        ValidatePath(group.App, $"{property}.app", manifestPath);
        ValidatePath(group.Pages, $"{property}.pages", manifestPath);
        ValidatePath(group.Content, $"{property}.content", manifestPath);
        ValidatePath(group.Images, $"{property}.images", manifestPath);
        ValidatePath(group.Fonts, $"{property}.fonts", manifestPath);
        ValidatePath(group.Media, $"{property}.media", manifestPath);
    }

    private static void ValidatePath(string value, string property, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Frontend manifest at {manifestPath} is missing a value for '{property}'.");
        }
    }
}
