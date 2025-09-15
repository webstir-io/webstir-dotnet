using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Engine.Pipelines.Core.Utilities;

namespace Engine.Pipelines.Core.Esbuild;

/// <summary>
/// Agnostic esbuild runner that executes esbuild with provided arguments.
/// Each pipeline (JS, CSS, etc.) is responsible for constructing their own arguments.
/// </summary>
public class EsbuildRunner(AppWorkspace workspace)
{
    private readonly AppWorkspace _workspace = workspace;

    /// <summary>
    /// Executes esbuild with the provided arguments and returns the result.
    /// </summary>
    public async Task<EsbuildResult> RunAsync(EsbuildOptions options, DiagnosticCollection? diagnostics = null)
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

        await ExecuteEsbuildAsync(args, diagnostics);

        return new EsbuildResult { OutputPath = options.OutputPath, Success = true };
    }

    private List<string> BuildArguments(EsbuildOptions options)
    {
        List<string> args = new List<string>();

        // Add entry points
        if (options.EntryPoints != null && options.EntryPoints.Count > 0)
        {
            args.AddRange(options.EntryPoints.Select(Quote));
        }

        // Add standard arguments
        if (options.Bundle)
            args.Add("--bundle");
        if (options.Minify)
            args.Add("--minify");
        if (options.Sourcemap)
            args.Add("--sourcemap");
        if (options.Splitting)
            args.Add("--splitting");
        if (options.AllowOverwrite)
            args.Add("--allow-overwrite");

        // Add output options
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

        // Add format
        if (options.Format != null)
        {
            args.Add($"--format={options.Format}");
        }

        // Add loaders
        if (options.Loaders != null)
        {
            foreach (KeyValuePair<string, string> loader in options.Loaders)
            {
                args.Add($"--loader:{loader.Key}={loader.Value}");
            }
        }

        // Add defines
        if (options.Define != null)
        {
            foreach (KeyValuePair<string, string> define in options.Define)
            {
                args.Add($"--define:{define.Key}={define.Value}");
            }
        }

        // Add custom arguments
        if (options.CustomArgs != null)
        {
            args.AddRange(options.CustomArgs);
        }

        return args;
    }

    private async Task ExecuteEsbuildAsync(List<string> args, DiagnosticCollection? diagnostics)
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
            HandleEsbuildError(process.ExitCode, stdOut, stdErr, diagnostics);
        }
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

    private static void HandleEsbuildError(int exitCode, string stdOut, string stdErr, DiagnosticCollection? diagnostics)
    {
        if (diagnostics != null)
        {
            // Let each pipeline handle error parsing with their own regex patterns
            diagnostics.Add(Diagnostic.Error($"{EsbuildConstants.Failed} with exit code {exitCode}: {stdErr}"));
        }
        else
        {
            throw new Exception(string.Format(CultureInfo.InvariantCulture, EsbuildConstants.FailedFormat, exitCode, stdErr, stdOut));
        }
    }

    private static string Quote(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (path.Contains(' '))
        {
            return FormattableString.Invariant($"\"{path}\"");
        }

        return path;
    }

}

