namespace Engine.Pipelines.JavaScript.Models;

public class JsBundleResult
{
    public required string Code { get; init; }
    public string? SourceMap { get; init; }
    public required string[] ModulePaths { get; init; }
}