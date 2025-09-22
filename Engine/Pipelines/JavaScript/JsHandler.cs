using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Esbuild;
using Engine.Pipelines.Core.Interfaces;
using Engine.Frontend;
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

    public async Task<bool> BuildAsync(string? changedFilePath = null)
    {
        bool compileSuccess = Build();
        if (!compileSuccess)
        {
            return false;
        }

        bool bundleSuccess = await BundleAsync(EsbuildMode.Development);
        return bundleSuccess;
    }

    public async Task<bool> PublishAsync() =>
        await BundleAsync(EsbuildMode.Production);

    public Task<bool> AddPageAsync(string pageName)
    {
        try
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
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError("[JS] Error creating page {PageName} - {Message}", pageName, ex.Message);
            return Task.FromResult(false);
        }
    }

    private bool Build()
    {
        bool compileSuccess = CompileTypeScriptFiles();
        if (!compileSuccess)
        {
            return false;
        }

        CopyRefreshScript();
        return true;
    }

    private async Task<bool> BundleAsync(EsbuildMode mode)
    {
        List<string> entryPoints = GetEntryPoints();
        if (entryPoints.Count == 0)
        {
            return true; // No entry points is not an error
        }

        string outDir = GetOutputDirectory(mode);

        bool isProduction = mode == EsbuildMode.Production;

        try
        {
            bool success = await _jsAdapter.BundleAsync(entryPoints, outDir, isProduction, _logger);

            if (!success)
            {
                return false;
            }

            if (isProduction)
            {
                await ProcessSplitBundlesAsync(outDir);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("[JS] Error during bundling - {Message}", ex.Message);
            return false;
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

    private bool CompileTypeScriptFiles()
    {
        string baseTsConfigPath = _workspace.WorkingPath.Combine(Files.BaseTsConfigJson);
        try
        {
            RunProcess(JsConstants.TscCommand, $"{JsConstants.TscBuildArg} \"{baseTsConfigPath}\"", JsConstants.TypeScriptCompilationDesc);
            return true;
        }
        catch (Exception ex)
        {
            // Parse TypeScript errors from the exception message
            ParseAndLogTypeScriptErrors(ex.Message);
            return false;
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
        if (!pagesRoot.Exists())
        {
            return [];
        }

        return [.. pagesRoot
            .Folders()
            .Select(d => d.Combine(Files.Index + FileExtensions.Js))
            .Where(File.Exists)];
    }

    private string GetOutputDirectory(EsbuildMode mode)
    {
        string outDir = mode == EsbuildMode.Development
            ? _workspace.FrontendBuildPath
            : _workspace.BuildPath.CreateSubDirectory(Folders.Temp).CreateSubDirectory(Folders.Frontend);

        return outDir.Create();
    }

    private async Task ProcessSplitBundlesAsync(string outDir)
    {
        string metaPath = outDir.Combine(EsbuildConstants.MetaJson);
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

    private void ParseAndLogTypeScriptErrors(string errorText)
    {
        if (string.IsNullOrWhiteSpace(errorText))
        {
            _logger.LogError("[JS] {Message}", JsConstants.TypeScriptCompilationFailed);
            return;
        }

        bool foundErrors = false;
        Regex[] patterns = [JsRegex.TscClassicError(), JsRegex.TscModernError()];

        foreach (Regex pattern in patterns)
        {
            foreach (Match match in pattern.Matches(errorText))
            {
                string file = match.Groups["file"].Value.Trim();
                int line = int.TryParse(match.Groups["line"].Value, out int ln) ? ln : 0;
                int col = int.TryParse(match.Groups["col"].Value, out int cl) ? cl : 0;
                string message = match.Groups["msg"].Value.Trim();

                if (!string.IsNullOrWhiteSpace(file) && line > 0 && col > 0)
                {
                    _logger.LogError("[JS] Error in {File}:{Line}:{Column} - {Message}",
                        file, line, col, message);
                }
                else if (!string.IsNullOrWhiteSpace(file))
                {
                    _logger.LogError("[JS] Error in {File} - {Message}", file, message);
                }
                else
                {
                    _logger.LogError("[JS] {Message}", message);
                }

                foundErrors = true;
            }
        }

        if (!foundErrors)
        {
            _logger.LogError("[JS] {Message}", JsConstants.TypeScriptCompilationFailed);
        }
    }

    private static async Task PrecompressJsFilesAsync(string root)
    {
        if (!root.Exists())
        {
            return;
        }

        List<Task> tasks = [];
        foreach (string file in root.Files("*.js", SearchOption.AllDirectories))
        {
            tasks.Add(Precompression.CreatePrecompressedVariantsAsync(file));
        }
        await Task.WhenAll(tasks);
    }
}
