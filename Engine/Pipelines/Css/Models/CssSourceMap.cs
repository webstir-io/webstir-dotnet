namespace Engine.Pipelines.Css.Models;

internal sealed record CssSourceMap
{
    public required int Version { get; init; }
    public required string File { get; init; }
    public required string[] Sources { get; init; }
    public required string?[] SourcesContent { get; init; }
    public required string Mappings { get; init; }
}
