using System.IO;
using System.Text.Json;
using Engine;

namespace Tester.Helpers;

public sealed class PageAssetManifest
{
    public string? Js
    {
        get; init;
    }

    public string? Css
    {
        get; init;
    }

    public static PageAssetManifest Load(string pageDistDirectory)
    {
        string manifestPath = Path.Combine(pageDistDirectory, Files.ManifestJson);
        if (!File.Exists(manifestPath))
        {
            return new PageAssetManifest();
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("pages", out JsonElement pagesElement))
            {
                foreach (JsonProperty page in pagesElement.EnumerateObject())
                {
                    JsonElement pageManifest = page.Value;
                    string? js = pageManifest.TryGetProperty("js", out JsonElement jsElement) ? jsElement.GetString() : null;
                    string? css = pageManifest.TryGetProperty("css", out JsonElement cssElement) ? cssElement.GetString() : null;
                    return new PageAssetManifest
                    {
                        Js = js,
                        Css = css
                    };
                }
            }
            else
            {
                string? js = root.TryGetProperty("js", out JsonElement jsElement) ? jsElement.GetString() : null;
                string? css = root.TryGetProperty("css", out JsonElement cssElement) ? cssElement.GetString() : null;
                return new PageAssetManifest
                {
                    Js = js,
                    Css = css
                };
            }
        }
        catch (JsonException)
        {
            // Ignore malformed manifest and fall back to defaults.
        }

        return new PageAssetManifest();
    }
}
