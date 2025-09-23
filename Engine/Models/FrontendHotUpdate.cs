using System;
using System.Collections.Generic;

namespace Engine.Models;

public sealed record FrontendHotUpdate
{
    public bool RequiresReload
    {
        get; init;
    }

    public IReadOnlyList<FrontendHotAsset> Modules
    {
        get; init;
    } = Array.Empty<FrontendHotAsset>();

    public IReadOnlyList<FrontendHotAsset> Styles
    {
        get; init;
    } = Array.Empty<FrontendHotAsset>();

    public string? ChangedFile
    {
        get; init;
    }

    public IReadOnlyList<string> FallbackReasons
    {
        get; init;
    } = Array.Empty<string>();

    public FrontendHotUpdateStats? Stats
    {
        get; init;
    }
}

public sealed record FrontendHotAsset
{
    public string Type
    {
        get; init;
    } = string.Empty;

    public string Path
    {
        get; init;
    } = string.Empty;

    public string RelativePath
    {
        get; init;
    } = string.Empty;

    public string Url
    {
        get; init;
    } = string.Empty;
}

public sealed record ChangeProcessingResult
{
    public static ChangeProcessingResult Empty
    {
        get;
    } = new ChangeProcessingResult();

    public FrontendHotUpdate? HotUpdate
    {
        get; init;
    }
}

public sealed record FrontendHotUpdateStats
{
    public int HotUpdates
    {
        get; init;
    }

    public int ReloadFallbacks
    {
        get; init;
    }
}
