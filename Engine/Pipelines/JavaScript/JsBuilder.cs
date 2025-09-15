using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions EsbuildMetaJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Build(DiagnosticCollection? diagnostics = null)
    {
        string packageJsonPath = _workspace.WorkingPath.Combine(Files.PackageJson);
        if (packageJsonPath.Exists())
            RunNpmInstall();

        CompileTypeScriptFiles(diagnostics);
        CopyRefreshScript();
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
            await ProcessSplitBundlesAsync(outDir);
        }
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
            ?? throw new Exception(string.Format(CultureInfo.InvariantCulture, JsConstants.FailedToStartFormat, description));

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


    private async Task ProcessSplitBundlesAsync(string outDir)
    {
        string metaPath = Path.Combine(outDir, JsConstants.MetaJson);
        string json = await File.ReadAllTextAsync(metaPath);
        JsonSerializerOptions options = EsbuildMetaJsonOptions;
        EsbuildMetafile? metafile = JsonSerializer.Deserialize<EsbuildMetafile>(json, options);
        File.Delete(metaPath);

        await outDir.CopyToAsync(_workspace.FrontendDistPath, recursive: true);

        if (metafile?.Outputs is null || metafile.Outputs.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<string, EsbuildOutputInfo> kvp in metafile.Outputs)
        {
            string outputPath = kvp.Key;
            EsbuildOutputInfo info = kvp.Value;
            if (string.IsNullOrWhiteSpace(info.EntryPoint))
            {
                continue;
            }

            string pageName = ExtractPageName(info.EntryPoint);
            if (string.IsNullOrWhiteSpace(pageName))
            {
                continue;
            }

            string entryFileName = Path.GetFileName(outputPath);
            string pageDistDir = _workspace.FrontendDistPath.Combine(Folders.Pages, pageName);
            AssetManifest.Update(pageDistDir, m => m.Js = entryFileName);
        }

        await PrecompressJsFilesAsync(_workspace.FrontendDistPath);
    }

    private string ExtractPageName(string entryPoint)
    {
        string basePages = _workspace.FrontendBuildPagesPath;
        string relative = Path.GetRelativePath(basePages, entryPoint);
        relative = relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        int sep = relative.IndexOf(Path.DirectorySeparatorChar);
        return sep > 0 ? relative[..sep] : string.Empty;
    }

    private static async Task PrecompressJsFilesAsync(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        List<Task> tasks = [];
        foreach (string file in Directory.EnumerateFiles(root, "*.js", SearchOption.AllDirectories))
        {
            tasks.Add(Precompression.CreatePrecompressedVariantsAsync(file));
        }
        await Task.WhenAll(tasks);
    }
}

internal sealed class EsbuildMetafile
{
    public Dictionary<string, EsbuildOutputInfo> Outputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class EsbuildOutputInfo
{
    public string? EntryPoint
    {
        get; set;
    }
}
