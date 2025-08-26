namespace Engine.Pipelines.Css.Models;

internal record CssMapping
{
    public required int GeneratedLine { get; init; }
    public required int GeneratedColumn { get; init; }
    public required int SourceIndex { get; init; }
    public required int OriginalLine { get; init; }
    public required int OriginalColumn { get; init; }
}