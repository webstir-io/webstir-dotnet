using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Engine.Pipelines.Core.Utilities;

namespace Engine.Pipelines.JavaScript;

public class EsbuildRunner(AppWorkspace workspace)
{
    private readonly AppWorkspace _workspace = workspace;

    public async Task BundleAsync(List<string> entryPoints, string outDir, EsbuildMode mode, DiagnosticCollection? diagnostics = null)
    {
        List<string> args = BuildBundleArguments(entryPoints, outDir, mode);
        await ExecuteEsbuildAsync(args, diagnostics);
    }

    public async Task<string> BundleSingleFileAsync(string inputFile, string outFile, EsbuildMode mode, bool minify, DiagnosticCollection? diagnostics = null)
    {
        List<string> args = BuildSingleFileArguments(inputFile, outFile, mode, minify);
        await ExecuteEsbuildAsync(args, diagnostics);

        string code = await File.ReadAllTextAsync(outFile);
        return code;
    }

    private List<string> BuildBundleArguments(List<string> entryPoints, string outDir, EsbuildMode mode)
    {
        string env = mode == EsbuildMode.Development ? JsConstants.EnvDevelopment : JsConstants.EnvProduction;

        List<string> args =
        [
            .. entryPoints.Select(Quote),
            JsConstants.EsbuildBundle,
            JsConstants.EsbuildFormatEsm,
            $"{JsConstants.EsbuildOutdir}{Quote(outDir)}",
            $"{JsConstants.EsbuildOutbase}{Quote(_workspace.FrontendBuildPath)}",
            $"{JsConstants.EsbuildDefineNodeEnv}\"{env}\""
        ];

        if (mode == EsbuildMode.Development)
        {
            args.Add(JsConstants.EsbuildSourcemap);
            args.Add(JsConstants.EsbuildAllowOverwrite);
        }
        else
        {
            args.Add(JsConstants.EsbuildMinify);
            args.Add(JsConstants.EsbuildDropConsole);
        }

        return args;
    }

    private static List<string> BuildSingleFileArguments(string inputFile, string outFile, EsbuildMode mode, bool minify)
    {
        string env = mode == EsbuildMode.Development ? JsConstants.EnvDevelopment : JsConstants.EnvProduction;

        List<string> args =
        [
            Quote(inputFile),
            JsConstants.EsbuildBundle,
            JsConstants.EsbuildFormatEsm,
            $"{JsConstants.EsbuildOutfile}{Quote(outFile)}",
            $"{JsConstants.EsbuildDefineNodeEnv}\"{env}\""
        ];

        if (minify)
        {
            args.Add(JsConstants.EsbuildMinify);
            args.Add(JsConstants.EsbuildDropConsole);
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
            ?? throw new Exception(JsConstants.FailedToStartEsbuild);

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
        string esbuildPath = Path.Combine(_workspace.NodeModulesPath, JsConstants.EsbuildBinFolder,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? JsConstants.EsbuildWindowsBinary : JsConstants.EsbuildBinary);

        if (!File.Exists(esbuildPath))
        {
            throw new FileNotFoundException(
                $"{JsConstants.EsbuildNotFoundPrefix}{esbuildPath}{JsConstants.EsbuildNotFoundSuffix}");
        }

        return esbuildPath;
    }

    private static void HandleEsbuildError(int exitCode, string stdOut, string stdErr, DiagnosticCollection? diagnostics)
    {
        if (diagnostics != null)
        {
            diagnostics.ParseAndAddErrors(stdErr, JsRegex.EsbuildError(), JsConstants.EsbuildFailed);
        }
        else
        {
            throw new Exception(string.Format(CultureInfo.InvariantCulture, JsConstants.EsbuildFailedFormat, exitCode, stdErr, stdOut));
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
            return $"\"{path}\"";
        }

        return path;
    }
}

