namespace Engine.Bundling.Transform;

public class TransformedModule
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public string? SourceMap { get; init; }
}

public class TransformContext
{
    public required int ModuleId { get; init; }
    public required string FilePath { get; init; }
    public required bool EnableScopeHoisting { get; init; }
    public required bool EnableTreeShaking { get; init; }
}