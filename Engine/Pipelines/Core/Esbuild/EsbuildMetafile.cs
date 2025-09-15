using System;
using System.Collections.Generic;

namespace Engine.Pipelines.Core.Esbuild;

/// <summary>
/// Represents the metafile output from esbuild containing build metadata.
/// </summary>
internal sealed class EsbuildMetafile
{
    public Dictionary<string, EsbuildOutputInfo> Outputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Represents information about a single output file in the esbuild metafile.
/// </summary>
internal sealed class EsbuildOutputInfo
{
    public string? EntryPoint
    {
        get; set;
    }
}
