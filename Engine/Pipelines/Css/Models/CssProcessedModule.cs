namespace Engine.Pipelines.Css.Models;

public record CssProcessedModule
{
    public required string Content
    {
        get; init;
    }
    public required Dictionary<string, string> ClassMappings
    {
        get; init;
    }
}
