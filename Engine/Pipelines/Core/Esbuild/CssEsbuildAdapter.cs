using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Pipelines.Core.Utilities;

namespace Engine.Pipelines.Core.Esbuild;

public class CssEsbuildAdapter(EsbuildRunner runner)
{
    private readonly EsbuildRunner _runner = runner;

    public async Task<string> BundleAsync(
        string entryPoint,
        string outputPath,
        bool isProduction,
        DiagnosticCollection? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        ArgumentNullException.ThrowIfNull(outputPath);

        // Get the directory containing the entry point to calculate relative path to app folder
        string? entryDir = Path.GetDirectoryName(entryPoint);
        string? appPath = entryDir != null ? Path.GetFullPath(Path.Combine(entryDir, "..", "..", "app")) : null;

        EsbuildOptions options = new()
        {
            EntryPoints = [entryPoint],
            Bundle = true,
            Loaders = new Dictionary<string, string>
            {
                [EsbuildConstants.ExtCss] = EsbuildConstants.LoaderCss,
                [EsbuildConstants.ExtModuleCss] = EsbuildConstants.LoaderLocalCss
            },
            CustomArgs = appPath != null ? [$"--alias:@app={appPath}"] : []
        };

        if (isProduction)
        {
            string? outputDir = Path.GetDirectoryName(outputPath);
            string outputName = Path.GetFileNameWithoutExtension(outputPath);
            options.OutputDir = outputDir;
            options.Minify = true;

            // Append entry-names to existing CustomArgs (which may contain the alias)
            List<string> customArgs = options.CustomArgs?.ToList() ?? [];
            customArgs.Add($"--entry-names={EsbuildConstants.EntryNamePattern}");
            options.CustomArgs = customArgs;

            await _runner.RunAsync(options, diagnostics);

            if (!string.IsNullOrEmpty(outputDir))
            {
                string pattern = $"{outputName}.{EsbuildConstants.CssFilePattern}";
                string? hashedFile = Directory.GetFiles(outputDir, pattern).FirstOrDefault();

                if (!string.IsNullOrEmpty(hashedFile))
                {
                    return hashedFile;
                }
            }

            throw new InvalidOperationException($"Failed to find generated CSS file for {entryPoint}");
        }
        else
        {
            options.OutputPath = outputPath;
            options.Sourcemap = true;

            await _runner.RunAsync(options, diagnostics);
            return outputPath;
        }
    }

    public async Task<List<string>> BundleMultipleAsync(
        List<string> cssFiles,
        string outputDir,
        bool isProduction,
        DiagnosticCollection? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(cssFiles);
        ArgumentNullException.ThrowIfNull(outputDir);

        List<string> outputPaths = [];

        foreach (string cssFile in cssFiles)
        {
            string outputName = Path.GetFileNameWithoutExtension(cssFile) + EsbuildConstants.ExtCss;
            string outputPath = Path.Combine(outputDir, outputName);
            string result = await BundleAsync(cssFile, outputPath, isProduction, diagnostics);
            outputPaths.Add(result);
        }

        return outputPaths;
    }
}
