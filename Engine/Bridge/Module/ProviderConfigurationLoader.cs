using System;
using System.IO;
using System.Text.Json;
using Engine.Models;

namespace Engine.Bridge.Module;

internal static class ProviderConfigurationLoader
{
    private const string ConfigurationFileName = "webstir.providers.json";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string? TryGetFrontendProvider(AppWorkspace workspace)
    {
        ProviderConfiguration? configuration = LoadConfiguration(workspace);
        return configuration?.Frontend;
    }

    public static string? TryGetBackendProvider(AppWorkspace workspace)
    {
        ProviderConfiguration? configuration = LoadConfiguration(workspace);
        return configuration?.Backend;
    }

    public static string? TryGetTestingProvider(AppWorkspace workspace)
    {
        ProviderConfiguration? configuration = LoadConfiguration(workspace);
        return configuration?.Testing;
    }

    private static ProviderConfiguration? LoadConfiguration(AppWorkspace workspace)
    {
        string configPath = Path.Combine(workspace.WorkingPath, ConfigurationFileName);
        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(configPath);
            ProviderConfiguration? configuration = JsonSerializer.Deserialize<ProviderConfiguration>(json, SerializerOptions);
            return configuration;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ProviderConfiguration
    {
        public string? Frontend
        {
            get; set;
        }

        public string? Backend
        {
            get; set;
        }

        public string? Testing
        {
            get; set;
        }
    }
}
