using System;
using System.IO;
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
    private readonly CssEsbuildAdapter _cssAdapter = new(new EsbuildRunner(workspace), new CssAutoprefixer(workspace));

    public int BuildOrder => 1;
    public int PublishOrder => 0;

    public async Task<bool> BuildAsync(string? changedFilePath = null) =>
        await ProcessPagesAsync(workspace.FrontendPagesPath, workspace.FrontendBuildPath, isProduction: false);

    public async Task<bool> PublishAsync() =>
        await ProcessPagesAsync(workspace.FrontendPagesPath, workspace.FrontendDistPath, isProduction: true);

    public Task<bool> AddPageAsync(string pageName)
    {
        try
        {
            string cssContent = $"""
                /* {pageName} Page Styles */
                @import "{CssConstants.AppImportAlias}";

                /* Add your page-specific styles here */

                """;
            string pageDirectory = workspace.FrontendPagesPath.Combine(pageName);
            string cssFilePath = pageDirectory.Combine($"{Files.Index}.css");
            File.WriteAllText(cssFilePath, cssContent);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError("[CSS] Error creating page {PageName} - {Message}", pageName, ex.Message);
            return Task.FromResult(false);
        }
    }

    private async Task<bool> ProcessPagesAsync(string sourceDir, string outputRootDir, bool isProduction)
    {
        if (!Directory.Exists(sourceDir))
        {
            return true; // No source directory is not an error
        }

        bool overallSuccess = true;
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

            try
            {
                string? producedPath = await _cssAdapter.BundleAsync(entryStylePath, outputFile, isProduction, _logger);

                if (producedPath == null)
                {
                    overallSuccess = false;
                    continue;
                }

                if (isProduction)
                {
                    await Precompression.CreatePrecompressedVariantsAsync(producedPath);
                    string cssFileName = Path.GetFileName(producedPath);
                    AssetManifest.Update(pageOutputDir, m => m.Css = cssFileName);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError("[CSS] Error in {Path} - {Message}", entryStylePath, ex.Message);
                overallSuccess = false;
            }
            catch (Exception ex)
            {
                _logger.LogError("[CSS] Error processing {Path} - {Message}", entryStylePath, ex.Message);
                overallSuccess = false;
            }
        }
        return overallSuccess;
    }

}
