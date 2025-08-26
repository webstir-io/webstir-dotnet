namespace Engine.Pipelines.Css.Models;

public record CssImport
{
    public required string Path { get; init; }
    public required string ResolvedPath { get; init; }
    public string? Media { get; init; }
    public bool IsModuleImport { get; init; }
}