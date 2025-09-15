using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Esbuild;
using Engine.Pipelines.Core.Interfaces;
using Engine.Pipelines.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.Css;

public class CssHandler(AppWorkspace workspace, ILogger<CssHandler> logger) : IPageHandler
{
    private readonly ILogger<CssHandler> _logger = logger;
    private readonly CssEsbuildAdapter _cssAdapter = new(new EsbuildRunner(workspace));

    public int BuildOrder => 1;
    public int PublishOrder => 0;

    public async Task BuildAsync(string? changedFilePath = null)
    {
        DiagnosticCollection diagnostics = new();
        await ProcessPagesAsync(workspace.FrontendPagesPath, workspace.FrontendBuildPath, isProduction: false, diagnostics);
        LogSummary("CSS Build", diagnostics);
    }

    public async Task PublishAsync() =>
        await ProcessPagesAsync(workspace.FrontendPagesPath, workspace.FrontendDistPath, isProduction: true);

    public Task AddPageAsync(string pageName)
    {
        string cssContent = $"""
            /* {pageName} Page Styles */
            @import "{CssConstants.AppImportAlias}";

            /* Add your page-specific styles here */

            """;
        string pageDirectory = workspace.FrontendPagesPath.Combine(pageName);
        string cssFilePath = pageDirectory.Combine($"{Files.Index}.css");
        File.WriteAllText(cssFilePath, cssContent);
        return Task.CompletedTask;
    }

    private async Task ProcessPagesAsync(string sourceDir, string outputRootDir, bool isProduction, DiagnosticCollection? diagnostics = null)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        foreach (string pageDir in sourceDir.Folders())
        {
            string pageName = pageDir.Filename();
            string moduleStylePath = pageDir.Combine($"{Files.Index}{CssConstants.ModuleExtension}{FileExtensions.Css}");
            string plainStylePath = pageDir.Combine($"{Files.Index}{FileExtensions.Css}");
            string? entryStylePath = moduleStylePath.Exists()
                ? moduleStylePath
                : (plainStylePath.Exists() ? plainStylePath : null);

            if (string.IsNullOrEmpty(entryStylePath))
            {
                continue;
            }

            string pageOutputDir = outputRootDir.Combine(Folders.Pages, pageName);
            pageOutputDir.Create();
            string outputFile = Path.Combine(pageOutputDir, $"{Files.Index}{FileExtensions.Css}");

            string producedPath = await _cssAdapter.BundleAsync(entryStylePath, outputFile, isProduction, diagnostics);

            if (isProduction)
            {
                await Precompression.CreatePrecompressedVariantsAsync(producedPath);
                string cssFileName = Path.GetFileName(producedPath);
                AssetManifest.Update(pageOutputDir, m => m.Css = cssFileName);
            }
        }
    }

    private void LogSummary(string phase, DiagnosticCollection diagnostics)
    {
        int errorCount = diagnostics.Errors.Count();
        int warningCount = diagnostics.Warnings.Count();
        if (errorCount == 0 && warningCount == 0)
        {
            return;
        }
        if (errorCount > 0)
        {
            _logger.LogError("{Phase} diagnostics: {Errors} errors, {Warnings} warnings", phase, errorCount, warningCount);
        }
        else
        {
            _logger.LogWarning("{Phase} diagnostics: {Warnings} warnings", phase, warningCount);
        }
    }
}
