using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Engine;

namespace Tests.Frontend;

public sealed class PageAssetManifest
{
    [JsonPropertyName("js")]
    public string? Js
    {
        get; init;
    }

    [JsonPropertyName("css")]
    public string? Css
    {
        get; init;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static PageAssetManifest Load(string pageDistDirectory)
    {
        string manifestPath = Path.Combine(pageDistDirectory, Files.ManifestJson);
        if (!File.Exists(manifestPath))
        {
            return new PageAssetManifest();
        }

        string json = File.ReadAllText(manifestPath);
        PageAssetManifest? manifest = JsonSerializer.Deserialize<PageAssetManifest>(json, SerializerOptions);
        return manifest ?? new PageAssetManifest();
    }
}
