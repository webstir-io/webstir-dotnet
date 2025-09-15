using System;
using System.IO;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.Html;

public class HtmlBuilder(AppWorkspace workspace, ILogger<HtmlBuilder> logger)
{
    private readonly ILogger<HtmlBuilder> _logger = logger;

    public async Task<bool> BuildAsync()
    {
        string appHtmlPath = workspace.FrontendAppPath.Combine(HtmlConstants.AppHtmlFileName);
        if (!appHtmlPath.Exists())
        {
            _logger.LogError("[HTML] Error in {Path} - Base application HTML file not found", appHtmlPath);
            return false;
        }

        string appTemplateHtml = await File.ReadAllTextAsync(appHtmlPath);

        // Validate template structure: require a <main> container to merge into
        if (!HtmlTransformer.HasMainSection(appTemplateHtml))
        {
            _logger.LogError("[HTML] Error in {Path} - Base template missing <main> container", appHtmlPath);
            return false;
        }

        bool success = await BuildPageHtmlFilesAsync(appTemplateHtml);
        return success;
    }

    public async Task<bool> PublishAsync() => await PublishPageHtmlAsync();

    private async Task<bool> BuildPageHtmlFilesAsync(string appTemplateHtml)
    {
        string pagesPath = workspace.FrontendPagesPath;
        if (!pagesPath.Exists())
            return true; // No pages to build is not an error

        bool overallSuccess = true;
        foreach (string pageDir in pagesPath.Folders())
        {
            string pageName = pageDir.Filename();
            bool success = await ProcessPageHtmlFilesAsync(pageDir, pageName, appTemplateHtml);
            overallSuccess &= success;
        }
        return overallSuccess;
    }

    private async Task<bool> ProcessPageHtmlFilesAsync(string pageDir, string pageName, string appTemplateHtml)
    {
        string[] htmlFiles = pageDir.Files($"*{FileExtensions.Html}");
        if (htmlFiles.Length == 0)
        {
            _logger.LogError("[HTML] Error in {Path} - No HTML fragments found under page directory", pageDir);
            return false;
        }

        bool overallSuccess = true;
        foreach (string htmlFile in htmlFiles)
        {
            string fileName = htmlFile.Filename();
            string outputName = fileName.Equals($"{Files.Index}{FileExtensions.Html}", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : fileName;

            bool success = await ProcessSingleHtmlFileAsync(htmlFile, pageName, outputName, appTemplateHtml);
            overallSuccess &= success;
        }
        return overallSuccess;
    }

    private async Task<bool> ProcessSingleHtmlFileAsync(string sourceFile, string pageName, string outputFileName, string appTemplateHtml)
    {
        string fragment = await File.ReadAllTextAsync(sourceFile);
        bool hasErrors = false;

        // Check for missing <head> or <main> fragments
        if (!HtmlTransformer.HasHeadSection(fragment))
        {
            _logger.LogError("[HTML] Error in {Path} - Page fragment missing <head> section", sourceFile);
            hasErrors = true;
        }
        if (!HtmlTransformer.HasMainSection(fragment))
        {
            _logger.LogError("[HTML] Error in {Path} - Page fragment missing <main> section", sourceFile);
            hasErrors = true;
        }

        if (hasErrors)
        {
            return false;
        }

        string mergedHtml = HtmlTransformer.MergeTemplates(appTemplateHtml, fragment);

        string outputDir = workspace.FrontendBuildPath
            .Combine(Folders.Pages, pageName);
        outputDir.Create();

        string outputPath = outputDir.Combine(outputFileName);
        await File.WriteAllTextAsync(outputPath, mergedHtml);
        return true;
    }

    private async Task<bool> PublishPageHtmlAsync()
    {
        string pagesPath = workspace.FrontendBuildPath.Combine(Folders.Pages);
        if (!pagesPath.Exists())
        {
            return true; // No pages to publish is not an error
        }

        bool overallSuccess = true;
        foreach (string pageDir in pagesPath.Folders())
        {
            string pageName = pageDir.Filename();
            string pageHtml = pageDir.Combine($"{Files.Index}{FileExtensions.Html}");

            if (!pageHtml.Exists())
            {
                _logger.LogWarning("[HTML] Warning - Missing page HTML: {Path}", pageHtml);
                continue;
            }

            try
            {
                await PublishHtmlFileAsync(pageHtml, pageName);
            }
            catch (Exception ex)
            {
                _logger.LogError("[HTML] Error in {Path} - Failed to publish: {Message}", pageHtml, ex.Message);
                overallSuccess = false;
            }
        }
        return overallSuccess;
    }

    private async Task PublishHtmlFileAsync(string sourceFile, string pageName)
    {
        string htmlContent = await File.ReadAllTextAsync(sourceFile);

        htmlContent = HtmlTransformer.RemoveRefreshScript(htmlContent);

        string pageDistDir = workspace.FrontendDistPath.Combine(Folders.Pages, pageName);
        AssetManifest manifest = AssetManifest.Load(pageDistDir);
        htmlContent = HtmlTransformer.RewriteAssetReferences(htmlContent, manifest, pageName);

        // Keep a single standard stylesheet link; no CSS preload/swap to avoid duplicate or broken loads.

        string pageBuildDir = workspace.FrontendBuildPath.Combine(Folders.Pages, pageName);
        htmlContent = HtmlTransformer.AddImageDimensions(htmlContent, pageBuildDir);
        htmlContent = Images.LazyLoadEnhancer.AddLazyLoading(htmlContent);
        htmlContent = await HtmlSecurityEnhancer.AddSRIForExternalResourcesAsync(htmlContent);
        htmlContent = ResourceHintInjector.Inject(htmlContent, manifest, pageName, workspace);
        htmlContent = Css.CriticalCssExtractor.InlineCriticalCss(htmlContent, pageName, pageDistDir);

        if (!string.IsNullOrWhiteSpace(manifest.Css))
        {
            string cssPath = Path.Combine(pageDistDir, manifest.Css);
            htmlContent = FontPreloadInjector.InjectFromCss(htmlContent, cssPath, pageName);
        }


        // Format HTML for consistent, readable output
        htmlContent = HtmlFormatter.FormatHtml(htmlContent);

        string distPagePath = workspace.FrontendDistPath.Combine(Folders.Pages, pageName, $"{Files.Index}{FileExtensions.Html}");
        distPagePath.DirectoryName().Create();

        await File.WriteAllTextAsync(distPagePath, htmlContent);
        await Precompression.CreatePrecompressedVariantsAsync(distPagePath);
    }
}
