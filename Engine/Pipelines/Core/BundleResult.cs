namespace Engine.Pipelines.Core;

public class BundleResult
{
    public required bool Success { get; init; }
    public List<OutputFile> Files { get; init; } = [];
    public DiagnosticCollection Diagnostics { get; init; } = new();
}

public class OutputFile
{
    public required string Path { get; init; }
    public required string Content { get; init; }
    public string? SourceMapPath { get; init; }
    public string? SourceMapContent { get; init; }
}

