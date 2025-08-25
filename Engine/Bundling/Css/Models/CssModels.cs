namespace Engine.Bundling.Css.Models;

public record CssModule
{
    public required string FilePath { get; init; }
    public required string Content { get; init; }
    public List<CssImport> Imports { get; init; } = [];
    public Dictionary<string, string> ClassMappings { get; init; } = [];
    public string? Hash { get; init; }
    public DateTime LastModified { get; init; }
}

public record CssImport
{
    public required string Path { get; init; }
    public required string ResolvedPath { get; init; }
    public string? Media { get; init; }
    public bool IsModuleImport { get; init; }
}

public record CssBundle
{
    public required string Content { get; init; }
    public Dictionary<string, Dictionary<string, string>> ModuleMappings { get; init; } = [];
    public string? SourceMap { get; init; }
}

public record ProcessedCssModule
{
    public required string Content { get; init; }
    public required Dictionary<string, string> ClassMappings { get; init; }
}

internal record Mapping
{
    public required int GeneratedLine { get; init; }
    public required int GeneratedColumn { get; init; }
    public required int SourceIndex { get; init; }
    public required int OriginalLine { get; init; }
    public required int OriginalColumn { get; init; }
}

internal record SourceMap
{
    public required int Version { get; init; }
    public required string File { get; init; }
    public required string[] Sources { get; init; }
    public required string?[] SourcesContent { get; init; }
    public required string Mappings { get; init; }
}