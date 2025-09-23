using System;
using System.Collections.Generic;
using System.Text.Json;
using Engine.Models;

namespace Engine.Bridge.Frontend;

internal static class FrontendHotUpdateParser
{
    public static bool TryCreateHotUpdate(FrontendCliDiagnostic diagnostic, out FrontendHotUpdate? hotUpdate)
    {
        hotUpdate = null;

        if (diagnostic.Data is not { Count: > 0 })
        {
            return false;
        }

        if (!TryGetDataProperty(diagnostic.Data, "hotUpdate", out JsonElement hotUpdateElement) ||
            hotUpdateElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetBoolean(hotUpdateElement, "requiresReload", out bool requiresReload))
        {
            return false;
        }

        List<FrontendHotAsset> modules = CreateAssetList(hotUpdateElement, "modules");
        List<FrontendHotAsset> styles = CreateAssetList(hotUpdateElement, "styles");
        string? changedFile = TryGetString(hotUpdateElement, "changedFile", out string? file) ? file : null;
        List<string> fallbackReasons = CreateStringList(hotUpdateElement, "fallbackReasons");
        FrontendHotUpdateStats? stats = TryCreateStats(hotUpdateElement, out FrontendHotUpdateStats? parsedStats) ? parsedStats : null;

        hotUpdate = new FrontendHotUpdate
        {
            RequiresReload = requiresReload,
            Modules = modules,
            Styles = styles,
            ChangedFile = changedFile,
            FallbackReasons = fallbackReasons,
            Stats = stats
        };

        return true;
    }

    private static bool TryGetDataProperty(Dictionary<string, JsonElement> data, string propertyName, out JsonElement value)
    {
        if (data.TryGetValue(propertyName, out value))
        {
            return true;
        }

        foreach (KeyValuePair<string, JsonElement> entry in data)
        {
            if (string.Equals(entry.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryCreateStats(JsonElement parent, out FrontendHotUpdateStats? stats)
    {
        stats = null;

        if (!TryGetProperty(parent, "stats", out JsonElement statsElement) || statsElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetInt32(statsElement, "hotUpdates", out int hotUpdates))
        {
            return false;
        }

        if (!TryGetInt32(statsElement, "reloadFallbacks", out int reloadFallbacks))
        {
            return false;
        }

        stats = new FrontendHotUpdateStats
        {
            HotUpdates = hotUpdates,
            ReloadFallbacks = reloadFallbacks
        };

        return true;
    }

    private static bool TryGetBoolean(JsonElement parent, string propertyName, out bool value)
    {
        value = false;

        if (!TryGetProperty(parent, propertyName, out JsonElement property))
        {
            return false;
        }

        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = property.GetBoolean();
            return true;
        }

        if (property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out bool parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool TryGetInt32(JsonElement parent, string propertyName, out int value)
    {
        value = 0;

        if (!TryGetProperty(parent, propertyName, out JsonElement property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int parsed))
        {
            value = parsed;
            return true;
        }

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool TryGetProperty(JsonElement parent, string propertyName, out JsonElement property)
    {
        property = default;

        if (parent.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (parent.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        foreach (JsonProperty candidate in parent.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetString(JsonElement parent, string propertyName, out string? value)
    {
        value = null;

        if (!TryGetProperty(parent, propertyName, out JsonElement property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        return false;
    }

    private static List<FrontendHotAsset> CreateAssetList(JsonElement parent, string propertyName)
    {
        List<FrontendHotAsset> assets = new();

        if (!TryGetProperty(parent, propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.Array)
        {
            return assets;
        }

        foreach (JsonElement item in property.EnumerateArray())
        {
            if (TryCreateHotAsset(item, out FrontendHotAsset asset))
            {
                assets.Add(asset);
            }
        }

        return assets;
    }

    private static bool TryCreateHotAsset(JsonElement element, out FrontendHotAsset asset)
    {
        asset = new FrontendHotAsset();

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetString(element, "type", out string? type) || string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        if (!TryGetString(element, "path", out string? path) || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!TryGetString(element, "relativePath", out string? relativePath) || string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        if (!TryGetString(element, "url", out string? url) || string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        asset = new FrontendHotAsset
        {
            Type = type,
            Path = path,
            RelativePath = relativePath,
            Url = url
        };

        return true;
    }

    private static List<string> CreateStringList(JsonElement parent, string propertyName)
    {
        List<string> values = new();

        if (!TryGetProperty(parent, propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.Array)
        {
            return values;
        }

        foreach (JsonElement item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? candidate = item.GetString();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    values.Add(candidate);
                }
            }
        }

        return values;
    }
}
