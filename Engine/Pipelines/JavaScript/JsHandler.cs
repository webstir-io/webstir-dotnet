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
using Engine.Pipelines.Core.Esbuild;
using Engine.Pipelines.Core.Interfaces;
using Engine.Pipelines.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.JavaScript;

public class JsHandler(AppWorkspace workspace, ILogger<JsHandler> logger) : IPageHandler
{
    private readonly AppWorkspace _workspace = workspace;
    private readonly ILogger<JsHandler> _logger = logger;
    private readonly JsEsbuildAdapter _jsAdapter = new(new EsbuildRunner(workspace), workspace);
    private static readonly JsonSerializerOptions EsbuildMetaJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public int BuildOrder => 0;
    public int PublishOrder => 0;

    public async Task BuildAsync(string? changedFilePath = null)
    {
        DiagnosticCollection diagnostics = new();
        Build(diagnostics);
        await BundleAsync(EsbuildMode.Development, diagnostics);
    }

    public async Task PublishAsync()
    {
        DiagnosticCollection diagnostics = new();
        await BundleAsync(EsbuildMode.Production, diagnostics);
    }

    public Task AddPageAsync(string pageName)
    {
        string pageDirectory = _workspace.FrontendPagesPath.CreateSubDirectory(pageName);
        string tsFilePath = pageDirectory.Combine($"{Files.Index}.ts");
        string tsContent = $"""
            // {pageName} page entry

            // Import shared app initialization (includes error handling)
            import '../../app/app';

            // Page-specific code here
            """;

        File.WriteAllText(tsFilePath, tsContent);
        return Task.CompletedTask;
    }

    private void Build(DiagnosticCollection? diagnostics = null)
    {
        CompileTypeScriptFiles(diagnostics);
        CopyRefreshScript();
    }

    private async Task BundleAsync(EsbuildMode mode, DiagnosticCollection? diagnostics = null)
    {
        List<string> entryPoints = GetEntryPoints();
        if (entryPoints.Count == 0)
        {
            return;
        }

        string outDir = GetOutputDirectory(mode);

        bool isProduction = mode == EsbuildMode.Production;
        await _jsAdapter.BundleAsync(entryPoints, outDir, isProduction, diagnostics);

        if (isProduction)
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
            _logger.LogWarning(JsConstants.RefreshJsNotFoundLog, Files.RefreshJs, sourceRefreshJsApp);
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
            : _workspace.BuildPath.CreateSubDirectory(Folders.Temp).CreateSubDirectory(Folders.Frontend);

        Directory.CreateDirectory(outDir);
        return outDir;
    }

    private async Task ProcessSplitBundlesAsync(string outDir)
    {
        string metaPath = Path.Combine(outDir, EsbuildConstants.MetaJson);
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
