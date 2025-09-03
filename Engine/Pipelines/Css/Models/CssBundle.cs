namespace Engine.Pipelines.Css.Models;

public record CssBundle
{
    public required string Content
    {
        get; init;
    }
    public Dictionary<string, Dictionary<string, string>> ModuleMappings { get; init; } = [];
    public string? SourceMap
    {
        get; init;
    }
}
