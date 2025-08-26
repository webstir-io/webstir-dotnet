namespace Engine.Pipelines.JavaScript.Models;

public class JsSourceMap
{
    public required int Version { get; init; }
    public required string[] Sources { get; init; }
    public required string[] Names { get; init; }
    public required string Mappings { get; init; }
}