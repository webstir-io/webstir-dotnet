using System;
using System.Collections.Generic;

namespace Engine.Pipelines.Css.Models;

public record CssModule
{
    public required string FilePath
    {
        get; init;
    }
    public required string Content
    {
        get; init;
    }
    public List<CssImport> Imports { get; init; } = [];
    public Dictionary<string, string> ClassMappings { get; init; } = [];
    public string? Hash
    {
        get; init;
    }
    public DateTime LastModified
    {
        get; init;
    }
}
