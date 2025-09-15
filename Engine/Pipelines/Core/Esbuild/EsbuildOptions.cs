using System.Collections.Generic;

namespace Engine.Pipelines.Core.Esbuild;

/// <summary>
/// Options for configuring esbuild execution.
/// </summary>
public class EsbuildOptions
{
    public List<string>? EntryPoints
    {
        get; set;
    }
    public string? OutputPath
    {
        get; set;
    }
    public string? OutputDir
    {
        get; set;
    }
    public string? Outbase
    {
        get; set;
    }
    public string? Format
    {
        get; set;
    }
    public bool Bundle
    {
        get; set;
    }
    public bool Minify
    {
        get; set;
    }
    public bool Sourcemap
    {
        get; set;
    }
    public bool Splitting
    {
        get; set;
    }
    public bool AllowOverwrite
    {
        get; set;
    }
    public Dictionary<string, string>? Loaders
    {
        get; set;
    }
    public Dictionary<string, string>? Define
    {
        get; set;
    }
    public List<string>? CustomArgs
    {
        get; set;
    }
}
