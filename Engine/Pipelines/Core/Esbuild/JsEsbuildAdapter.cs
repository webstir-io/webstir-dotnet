using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Engine.Pipelines.Core.Utilities;

namespace Engine.Pipelines.Core.Esbuild;

/// <summary>
/// Adapter that configures esbuild specifically for JavaScript bundling.
/// </summary>
public class JsEsbuildAdapter(EsbuildRunner runner, AppWorkspace workspace)
{
    public async Task<EsbuildResult> BundleAsync(
        List<string> entryPoints,
        string outputDir,
        bool isProduction,
        DiagnosticCollection? diagnostics = null)
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
            options.CustomArgs =
            [
                EsbuildConstants.DropConsole,
                $"{EsbuildConstants.ChunkNames}{EsbuildConstants.ChunksFolder}/{EsbuildConstants.ChunkNamePattern}",
                $"{EsbuildConstants.EntryNames}{EsbuildConstants.EntryDirPattern}/{EsbuildConstants.EntryNamePattern}",
                $"{EsbuildConstants.Metafile}{Path.Combine(outputDir, EsbuildConstants.MetaJson)}"
            ];
        }
        else
        {
            options.Sourcemap = true;
            options.AllowOverwrite = true;
        }

        return await runner.RunAsync(options, diagnostics);
    }
}
