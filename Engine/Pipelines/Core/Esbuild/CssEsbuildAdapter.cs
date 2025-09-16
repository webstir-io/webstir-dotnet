using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.Core.Esbuild;

public class CssEsbuildAdapter(EsbuildRunner runner, CssAutoprefixer autoprefixer)
{
    private readonly EsbuildRunner _runner = runner;
    private readonly CssAutoprefixer _autoprefixer = autoprefixer;

    public async Task<string?> BundleAsync(
        string entryPoint,
        string outputPath,
        bool isProduction,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        ArgumentNullException.ThrowIfNull(outputPath);

        EsbuildOptions options = CreateBaseOptions(entryPoint);

        if (isProduction)
        {
            return await BundleForProductionAsync(options, outputPath, entryPoint, logger);
        }
        else
        {
            return await BundleForDevelopmentAsync(options, outputPath, logger);
        }
    }

    private static EsbuildOptions CreateBaseOptions(string entryPoint)
    {
        string? appPath = GetAppPath(entryPoint);

        return new EsbuildOptions
        {
            EntryPoints = [entryPoint],
            Bundle = true,
            Loaders = CreateCssLoaders(),
            Alias = CreateAppAlias(appPath)
        };
    }

    private static string? GetAppPath(string entryPoint)
    {
        string? entryDir = Path.GetDirectoryName(entryPoint);
        return entryDir != null
            ? Path.GetFullPath(Path.Combine(entryDir, "..", "..", "app"))
            : null;
    }

    private static Dictionary<string, string> CreateCssLoaders() => new()
    {
        [EsbuildConstants.ExtCss] = EsbuildConstants.LoaderCss,
        [EsbuildConstants.ExtModuleCss] = EsbuildConstants.LoaderLocalCss
    };

    private static Dictionary<string, string>? CreateAppAlias(string? appPath) =>
        appPath != null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["@app"] = appPath
            }
            : null;

    private async Task<string?> BundleForProductionAsync(
        EsbuildOptions options,
        string outputPath,
        string entryPoint,
        ILogger? logger)
    {
        ConfigureProductionOptions(options, outputPath);

        EsbuildResult result = await _runner.RunAsync(options, logger);
        if (!result.Success)
        {
            return null;
        }

        string? cssFile = FindGeneratedCssFile(outputPath, entryPoint, logger);
        if (cssFile == null)
        {
            return null;
        }

        bool prefixed = await _autoprefixer.ApplyAsync(cssFile, logger);
        return prefixed ? cssFile : null;
    }

    private static void ConfigureProductionOptions(EsbuildOptions options, string outputPath)
    {
        options.OutputDir = Path.GetDirectoryName(outputPath);
        options.Minify = true;
        options.EntryNames = EsbuildConstants.EntryNamePattern;
    }

    private static string? FindGeneratedCssFile(
        string outputPath,
        string entryPoint,
        ILogger? logger)
    {
        string? outputDir = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(outputDir))
        {
            return null;
        }

        string outputName = Path.GetFileNameWithoutExtension(outputPath);
        string pattern = $"{outputName}.{EsbuildConstants.CssFilePattern}";
        string? hashedFile = Directory.GetFiles(outputDir, pattern).FirstOrDefault();

        if (!string.IsNullOrEmpty(hashedFile))
        {
            return hashedFile;
        }

        logger?.LogError("[CSS] Failed to find generated CSS file for {EntryPoint}", entryPoint);
        return null;
    }

    private async Task<string?> BundleForDevelopmentAsync(
        EsbuildOptions options,
        string outputPath,
        ILogger? logger)
    {
        options.OutputPath = outputPath;
        options.Sourcemap = true;

        EsbuildResult result = await _runner.RunAsync(options, logger);
        if (!result.Success)
        {
            return null;
        }

        bool prefixed = await _autoprefixer.ApplyAsync(outputPath, logger);
        return prefixed ? outputPath : null;
    }

    public async Task<List<string?>> BundleMultipleAsync(
        List<string> cssFiles,
        string outputDir,
        bool isProduction,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(cssFiles);
        ArgumentNullException.ThrowIfNull(outputDir);

        List<string?> outputPaths = [];

        foreach (string cssFile in cssFiles)
        {
            string outputName = Path.GetFileNameWithoutExtension(cssFile) + EsbuildConstants.ExtCss;
            string outputPath = Path.Combine(outputDir, outputName);
            string? result = await BundleAsync(cssFile, outputPath, isProduction, logger);
            outputPaths.Add(result);
        }

        return outputPaths;
    }
}
