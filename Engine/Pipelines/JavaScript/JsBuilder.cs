using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Utilities;

using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.JavaScript;

public class JsBuilder(AppWorkspace workspace, ILogger<JsBuilder> logger)
{
    private readonly AppWorkspace _workspace = workspace;
    private readonly EsbuildRunner _esbuildRunner = new(workspace);

    public void Build(DiagnosticCollection? diagnostics = null)
    {
        string packageJsonPath = _workspace.WorkingPath.Combine(Files.PackageJson);
        if (packageJsonPath.Exists())
            RunNpmInstall();

        CompileTypeScriptFiles(diagnostics);
        CopyRefreshScript();
        CopyErrorScript();
    }

    public async Task BundleAsync(EsbuildMode mode, DiagnosticCollection? diagnostics = null)
    {
        List<string> entryPoints = GetEntryPoints();
        if (entryPoints.Count == 0)
        {
            return;
        }

        string outDir = GetOutputDirectory(mode);
        await _esbuildRunner.BundleAsync(entryPoints, outDir, mode, diagnostics!);

        if (mode == EsbuildMode.Production)
        {
            await FingerprintAndMoveAsync(outDir);
        }
    }

    public async Task<string> BundleSingleFileAsync(string inputFile, EsbuildMode mode, bool minify)
    {
        ArgumentNullException.ThrowIfNull(inputFile);
        if (!File.Exists(inputFile))
        {
            throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, JsConstants.InputFileNotFoundFormat, inputFile));
        }

        string outDir = _workspace.BuildPath.CreateSubDirectory(JsConstants.TempFolder).CreateSubDirectory(JsConstants.SingleFolder);
        Directory.CreateDirectory(outDir);
        string outFile = Path.Combine(outDir, Path.GetFileName(inputFile));

        return await _esbuildRunner.BundleSingleFileAsync(inputFile, outFile, mode, minify, diagnostics: null);
    }

    private void CopyRefreshScript()
    {
        string sourceRefreshJsApp = _workspace.FrontendAppPath.Combine(Files.RefreshJs);
        string targetRefreshJs = _workspace.FrontendBuildPath.Combine(Files.RefreshJs);

        if (sourceRefreshJsApp.Exists())
            File.Copy(sourceRefreshJsApp, targetRefreshJs, true);
        else
            logger.LogWarning(JsConstants.RefreshJsNotFoundLog, Files.RefreshJs, sourceRefreshJsApp);
    }

    private void CopyErrorScript()
    {
        string compiledErrorJs = _workspace.FrontendBuildAppPath.Combine(JsConstants.ErrorJs);
        if (!compiledErrorJs.Exists())
        {
            logger.LogWarning(JsConstants.CompiledErrorJsNotFoundLog, compiledErrorJs);
        }
    }

    private void CompileTypeScriptFiles(DiagnosticCollection? diagnostics)
    {
        string baseTsConfigPath = _workspace.WorkingPath.Combine(Files.BaseTsConfigJson);
        try
        {
            RunProcess(JsConstants.TscCommand, $"{JsConstants.TscBuildArg} \"{baseTsConfigPath}\"", JsConstants.TypeScriptCompilationDesc);
        }
        catch (Exception ex)
        {
            if (diagnostics != null)
            {
                diagnostics.ParseAndAddErrors(ex.Message,
                    [JsRegex.TscClassicError(), JsRegex.TscModernError()],
                    JsConstants.TypeScriptCompilationFailed);
            }
            else
            {
                throw;
            }
        }
    }

    private void RunNpmInstall()
    {
        string packageLockPath = _workspace.WorkingPath.Combine(Files.PackageLockJson);
        string npmCommand = packageLockPath.Exists() ? JsConstants.NpmCiArg : JsConstants.NpmInstallArg;
        RunProcess(JsConstants.NpmCommand, npmCommand, JsConstants.NpmInstallDesc, _workspace.WorkingPath);
    }

    private static void RunProcess(string fileName, string arguments, string description, string? workingDirectory = null)
    {
        ProcessStartInfo processInfo = new()
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        using Process process = Process.Start(processInfo)
            ?? throw new Exception(string.Format(CultureInfo.InvariantCulture, ProcessCommands.FailedToStartFormat, description));

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string errors = process.StandardError.ReadToEnd();
            string output = process.StandardOutput.ReadToEnd();
            string errorMessage = string.Format(CultureInfo.InvariantCulture, JsConstants.ProcessFailedFormat, description, process.ExitCode);

            if (!string.IsNullOrWhiteSpace(errors))
                errorMessage += $"{JsConstants.ErrorsHeader}{errors}";
            if (!string.IsNullOrWhiteSpace(output))
                errorMessage += $"{JsConstants.OutputHeader}{output}";

            throw new Exception(errorMessage);
        }
    }

    private List<string> GetEntryPoints()
    {
        string pagesRoot = _workspace.FrontendBuildPagesPath;
        if (!Directory.Exists(pagesRoot))
        {
            return [];
        }

        return [.. Directory
            .EnumerateDirectories(pagesRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(d => Path.Combine(d, Files.Index + FileExtensions.Js))
            .Where(File.Exists)];
    }

    private string GetOutputDirectory(EsbuildMode mode)
    {
        string outDir = mode == EsbuildMode.Development
            ? _workspace.FrontendBuildPath
            : _workspace.BuildPath.CreateSubDirectory(JsConstants.TempFolder).CreateSubDirectory(Folders.Frontend);

        Directory.CreateDirectory(outDir);
        return outDir;
    }


    private async Task FingerprintAndMoveAsync(string outDir)
    {
        string pagesRoot = Path.Combine(outDir, Folders.Pages);
        if (!Directory.Exists(pagesRoot))
        {
            return;
        }

        foreach (string pageDir in Directory.EnumerateDirectories(pagesRoot, "*", SearchOption.TopDirectoryOnly))
        {
            string pageName = Path.GetFileName(pageDir);
            string builtJs = Path.Combine(pageDir, Files.Index + FileExtensions.Js);
            if (!File.Exists(builtJs))
            {
                continue;
            }

            string code = await File.ReadAllTextAsync(builtJs);
            code = StripSourceMapComments(code);

            string hash = ContentHashGenerator.ComputeHash(code);
            string jsFileName = $"{Files.Index}.{hash}{FileExtensions.Js}";

            string pageDistDir = _workspace.FrontendDistPath.Combine(Folders.Pages, pageName);
            pageDistDir.Create();

            string distJsPath = Path.Combine(pageDistDir, jsFileName);
            await File.WriteAllTextAsync(distJsPath, code);
            await Precompression.CreatePrecompressedVariantsAsync(distJsPath);

            AssetManifest.Update(pageDistDir, m => m.Js = jsFileName);
        }
    }

    private static string StripSourceMapComments(string js)
    {
        js = JsRegex.SourceMapLine().Replace(js, string.Empty);
        js = JsRegex.SourceMapBlock().Replace(js, string.Empty);

        return js;
    }
}
