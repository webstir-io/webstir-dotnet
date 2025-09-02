using System.Text.Json;

namespace Engine.Pipelines.Core;

public sealed class AssetManifest
{
    public string? Js { get; set; }
    public string? Css { get; set; }

    public MapFiles Map { get; set; } = new();

    public sealed class MapFiles
    {
        public string? Js { get; set; }
        public string? Css { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static AssetManifest Load(string pageDistDirectory)
    {
        ArgumentNullException.ThrowIfNull(pageDistDirectory);
        string manifestPath = Path.Combine(pageDistDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return new AssetManifest();
        }

        try
        {
            string json = File.ReadAllText(manifestPath);
            AssetManifest? manifest = JsonSerializer.Deserialize<AssetManifest>(json, JsonOptions);
            return manifest ?? new AssetManifest();
        }
        catch
        {
            return new AssetManifest();
        }
    }

    public void Save(string pageDistDirectory)
    {
        ArgumentNullException.ThrowIfNull(pageDistDirectory);
        Directory.CreateDirectory(pageDistDirectory);
        string manifestPath = Path.Combine(pageDistDirectory, "manifest.json");
        string json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(manifestPath, json);
    }
}
