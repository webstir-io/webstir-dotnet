using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.Core.Esbuild;

public class JsEsbuildAdapter(EsbuildRunner runner, AppWorkspace workspace)
{
    public async Task<bool> BundleAsync(
        List<string> entryPoints,
        string outputDir,
        bool isProduction,
        ILogger logger)
    {
        EsbuildOptions options = new()
        {
            EntryPoints = entryPoints,
            OutputDir = outputDir,
            Outbase = workspace.FrontendBuildPath,
            Format = EsbuildConstants.FormatEsm,
            Bundle = true,
            Define = new Dictionary<string, string>
            {
                ["process.env.NODE_ENV"] = isProduction
                    ? $"\"{EsbuildConstants.EnvProduction}\""
                    : $"\"{EsbuildConstants.EnvDevelopment}\""
            }
        };

        if (isProduction)
        {
            options.Minify = true;
            options.Splitting = true;
            options.Drop = ["console"];
            options.ChunkNames = FormattableString.Invariant($"{EsbuildConstants.ChunksFolder}/{EsbuildConstants.ChunkNamePattern}");
            options.EntryNames = FormattableString.Invariant($"{EsbuildConstants.EntryDirPattern}/{EsbuildConstants.EntryNamePattern}");
            options.MetafilePath = Path.Combine(outputDir, EsbuildConstants.MetaJson);
        }
        else
        {
            options.Sourcemap = true;
            options.AllowOverwrite = true;
        }

        EsbuildResult result = await runner.RunAsync(options, logger);
        return result.Success;
    }
}
