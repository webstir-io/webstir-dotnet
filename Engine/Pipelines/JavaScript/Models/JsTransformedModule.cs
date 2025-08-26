namespace Engine.Pipelines.JavaScript.Models;

public class JsTransformedModule
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public string? SourceMap { get; init; }
}