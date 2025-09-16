using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.Core.Esbuild;

public class EsbuildRunner(AppWorkspace workspace)
{
    private readonly AppWorkspace _workspace = workspace;

    public async Task<EsbuildResult> RunAsync(EsbuildOptions options, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> args = BuildArguments(options);

        if (options.OutputPath != null)
        {
            string? outDirectory = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrWhiteSpace(outDirectory))
            {
                Directory.CreateDirectory(outDirectory);
            }
        }

        bool success = await ExecuteEsbuildAsync(args, logger);

        return new EsbuildResult { OutputPath = options.OutputPath, Success = success };
    }

    private static List<string> BuildArguments(EsbuildOptions options)
    {
        List<string> args = [];

        if (options.EntryPoints?.Count > 0)
        {
            args.AddRange(options.EntryPoints.Select(Quote));
        }

        AddBooleanFlags(args, options);
        AddOutputOptions(args, options);
        AddConfigOptions(args, options);

        if (options.CustomArgs != null)
        {
            args.AddRange(options.CustomArgs);
        }

        return args;
    }

    private static void AddBooleanFlags(List<string> args, EsbuildOptions options)
    {
        if (options.Bundle)
        {
            args.Add("--bundle");
        }
        if (options.Minify)
        {
            args.Add("--minify");
        }
        if (options.Sourcemap)
        {
            args.Add("--sourcemap");
        }
        if (options.Splitting)
        {
            args.Add("--splitting");
        }
        if (options.AllowOverwrite)
        {
            args.Add("--allow-overwrite");
        }
    }

    private static void AddOutputOptions(List<string> args, EsbuildOptions options)
    {
        if (options.OutputPath != null)
        {
            args.Add($"--outfile={Quote(options.OutputPath)}");
        }
        else if (options.OutputDir != null)
        {
            args.Add($"--outdir={Quote(options.OutputDir)}");
        }

        if (options.Outbase != null)
        {
            args.Add($"--outbase={Quote(options.Outbase)}");
        }

        if (!string.IsNullOrEmpty(options.EntryNames))
        {
            args.Add($"--entry-names={options.EntryNames}");
        }

        if (!string.IsNullOrEmpty(options.ChunkNames))
        {
            args.Add($"--chunk-names={options.ChunkNames}");
        }

        if (!string.IsNullOrEmpty(options.MetafilePath))
        {
            args.Add($"--metafile={Quote(options.MetafilePath)}");
        }

        if (options.Drop is { Count: > 0 })
        {
            foreach (string item in options.Drop)
            {
                args.Add($"--drop:{item}");
            }
        }
    }

    private static void AddConfigOptions(List<string> args, EsbuildOptions options)
    {
        if (options.Format != null)
        {
            args.Add($"--format={options.Format}");
        }

        if (options.Loaders != null)
        {
            foreach (KeyValuePair<string, string> loader in options.Loaders)
            {
                args.Add($"--loader:{loader.Key}={loader.Value}");
            }
        }

        if (options.Define != null)
        {
            foreach (KeyValuePair<string, string> define in options.Define)
            {
                args.Add($"--define:{define.Key}={define.Value}");
            }
        }

        if (options.Alias != null)
        {
            foreach (KeyValuePair<string, string> alias in options.Alias)
            {
                args.Add($"--alias:{alias.Key}={Quote(alias.Value)}");
            }
        }
    }

    private async Task<bool> ExecuteEsbuildAsync(List<string> args, ILogger? logger)
    {
        string esbuildPath = GetEsbuildPath();
        string builtArgs = string.Join(' ', args);

        ProcessStartInfo psi = new()
        {
            FileName = esbuildPath,
            Arguments = builtArgs,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(psi)
            ?? throw new Exception(EsbuildConstants.FailedToStart);

        string stdOut = await process.StandardOutput.ReadToEndAsync();
        string stdErr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            HandleEsbuildError(process.ExitCode, stdOut, stdErr, logger);
            return false;
        }

        return true;
    }

    private string GetEsbuildPath()
    {
        string esbuildPath = Path.Combine(
            _workspace.NodeModulesPath,
            EsbuildConstants.BinFolder,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? EsbuildConstants.WindowsBinary : EsbuildConstants.Binary);

        if (!File.Exists(esbuildPath))
        {
            throw new FileNotFoundException($"{EsbuildConstants.NotFoundPrefix}{esbuildPath}{EsbuildConstants.NotFoundSuffix}");
        }

        return esbuildPath;
    }

    private static void HandleEsbuildError(int exitCode, string stdOut, string stdErr, ILogger? logger)
    {
        if (logger != null)
        {
            logger.LogError("[Esbuild] Failed with exit code {ExitCode}: {Error}", exitCode, stdErr);
            if (!string.IsNullOrWhiteSpace(stdOut))
            {
                logger.LogError("[Esbuild] Output: {Output}", stdOut);
            }
        }
        else
        {
            throw new Exception(string.Format(CultureInfo.InvariantCulture, EsbuildConstants.FailedFormat, exitCode, stdErr, stdOut));
        }
    }

    private static string Quote(string path) =>
        string.IsNullOrEmpty(path) || !path.Contains(' ')
            ? path
            : FormattableString.Invariant($"\"{path}\"");
}

