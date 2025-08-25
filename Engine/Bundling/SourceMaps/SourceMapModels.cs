namespace Engine.Bundling.SourceMaps;

public class SourceMap
{
    public required int Version { get; init; }
    public required string[] Sources { get; init; }
    public required string[] Names { get; init; }
    public required string Mappings { get; init; }
}

public class MappingSegment
{
    public required int GeneratedLine { get; init; }
    public required int GeneratedColumn { get; init; }
    public required int SourceIndex { get; init; }
    public required int OriginalLine { get; init; }
    public required int OriginalColumn { get; init; }
    public int? NameIndex { get; init; }
}