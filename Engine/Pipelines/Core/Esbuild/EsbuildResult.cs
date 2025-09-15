namespace Engine.Pipelines.Core.Esbuild;

/// <summary>
/// Result from esbuild execution.
/// </summary>
public class EsbuildResult
{
    public bool Success
    {
        get; set;
    }
    public string? OutputPath
    {
        get; set;
    }
}
