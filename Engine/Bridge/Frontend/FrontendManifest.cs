using System.Text.Json.Serialization;

namespace Engine.Bridge.Frontend;

public sealed class FrontendManifest
{
    [JsonPropertyName("version")]
    public int Version
    {
        get; init;
    }

    [JsonPropertyName("paths")]
    public required FrontendManifestPaths Paths
    {
        get; init;
    }

    [JsonPropertyName("features")]
    public required FrontendManifestFeatures Features
    {
        get; init;
    }
}

public sealed class FrontendManifestPaths
{
    [JsonPropertyName("workspace")]
    public required string Workspace
    {
        get; init;
    }

    [JsonPropertyName("src")]
    public required FrontendManifestPathGroup Src
    {
        get; init;
    }

    [JsonPropertyName("build")]
    public required FrontendManifestPathGroup Build
    {
        get; init;
    }

    [JsonPropertyName("dist")]
    public required FrontendManifestPathGroup Dist
    {
        get; init;
    }
}

public sealed class FrontendManifestPathGroup
{
    [JsonPropertyName("root")]
    public required string Root
    {
        get; init;
    }

    [JsonPropertyName("frontend")]
    public required string Frontend
    {
        get; init;
    }

    [JsonPropertyName("app")]
    public required string App
    {
        get; init;
    }

    [JsonPropertyName("pages")]
    public required string Pages
    {
        get; init;
    }

    [JsonPropertyName("content")]
    public required string Content
    {
        get; init;
    }

    [JsonPropertyName("images")]
    public required string Images
    {
        get; init;
    }

    [JsonPropertyName("fonts")]
    public required string Fonts
    {
        get; init;
    }

    [JsonPropertyName("media")]
    public required string Media
    {
        get; init;
    }
}

public sealed class FrontendManifestFeatures
{
    [JsonPropertyName("htmlSecurity")]
    public bool HtmlSecurity
    {
        get; init;
    }

    [JsonPropertyName("imageOptimization")]
    public bool ImageOptimization
    {
        get; init;
    }

    [JsonPropertyName("precompression")]
    public bool Precompression
    {
        get; init;
    }
}
