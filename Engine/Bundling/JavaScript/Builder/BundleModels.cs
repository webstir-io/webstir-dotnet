namespace Engine.Bundling.JavaScript.Builder;

public class BundleOptions
{
    public bool GenerateSourceMap { get; init; } = true;
    public bool EnableScopeHoisting { get; init; } = true;
    public bool EnableTreeShaking { get; init; } = true;
    public bool Minify { get; init; }
    public string OutputPath { get; init; } = string.Empty;
}

public class BundleResult
{
    public required string Code { get; init; }
    public string? SourceMap { get; init; }
    public required string[] ModulePaths { get; init; }
}