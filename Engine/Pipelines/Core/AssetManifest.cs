using System.Collections.Concurrent;
using System.Text.Json;

namespace Engine.Pipelines.Core;

public sealed class AssetManifest
{
    public string? Js
    {
        get; set;
    }
    public string? Css
    {
        get; set;
    }

    public MapFiles Map { get; set; } = new();

    public sealed class MapFiles
    {
        public string? Js
        {
            get; set;
        }
        public string? Css
        {
            get; set;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly ConcurrentDictionary<string, object> Locks = new(StringComparer.OrdinalIgnoreCase);

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

    public static void Update(string pageDistDirectory, Action<AssetManifest> updateAction)
    {
        ArgumentNullException.ThrowIfNull(pageDistDirectory);
        ArgumentNullException.ThrowIfNull(updateAction);

        string key = Path.GetFullPath(pageDistDirectory);
        object gate = Locks.GetOrAdd(key, _ => new object());

        lock (gate)
        {
            AssetManifest manifest = Load(pageDistDirectory);
            updateAction(manifest);
            manifest.Save(pageDistDirectory);
        }
    }

    public void Save(string pageDistDirectory)
    {
        ArgumentNullException.ThrowIfNull(pageDistDirectory);
        Directory.CreateDirectory(pageDistDirectory);
        string manifestPath = Path.Combine(pageDistDirectory, "manifest.json");
        string json = JsonSerializer.Serialize(this, JsonOptions);
        string tempPath = Path.Combine(pageDistDirectory, $"manifest.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, manifestPath, overwrite: true);
    }
}
