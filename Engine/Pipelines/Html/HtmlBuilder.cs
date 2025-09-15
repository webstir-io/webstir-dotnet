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

    public async Task BuildAsync(DiagnosticCollection? diagnostics = null)
    {
        DiagnosticCollection diag = diagnostics ?? new DiagnosticCollection();
        string appHtmlPath = workspace.FrontendAppPath.Combine(HtmlConstants.AppHtmlFileName);
        if (!appHtmlPath.Exists())
        {
            _logger.LogError("Base application HTML file not found: {AppHtmlPath}", appHtmlPath);
            diag.Add(Diagnostic.Error($"Base application HTML file not found: {appHtmlPath}", appHtmlPath));
            return;
        }

        string appTemplateHtml = await File.ReadAllTextAsync(appHtmlPath);

        // Validate template structure: require a <main> container to merge into
        if (!HtmlTransformer.HasMainSection(appTemplateHtml))
        {
            _logger.LogError("Base template missing <main> container: {AppHtmlPath}", appHtmlPath);
            diag.Add(Diagnostic.Error("Base template missing <main> container", appHtmlPath));
        }

        await BuildPageHtmlFilesAsync(appTemplateHtml, diag);

        if (diag.HasErrors)
        {
            throw new InvalidOperationException("HTML build failed due to errors in page fragments or base template.");
        }
    }

    public async Task PublishAsync(DiagnosticCollection? diagnostics = null) => await PublishPageHtmlAsync(diagnostics);

    private async Task BuildPageHtmlFilesAsync(string appTemplateHtml, DiagnosticCollection diagnostics)
    {
        string pagesPath = workspace.FrontendPagesPath;
        if (!pagesPath.Exists())
            return;

        foreach (string pageDir in pagesPath.Folders())
        {
            string pageName = pageDir.Filename();
            await ProcessPageHtmlFilesAsync(pageDir, pageName, appTemplateHtml, diagnostics);
        }
    }

    private async Task ProcessPageHtmlFilesAsync(string pageDir, string pageName, string appTemplateHtml, DiagnosticCollection diagnostics)
    {
        string[] htmlFiles = pageDir.Files($"*{FileExtensions.Html}");
        if (htmlFiles.Length == 0)
        {
            throw new InvalidOperationException($"No HTML fragments found under page directory: {pageDir}");
        }

        foreach (string htmlFile in htmlFiles)
        {
            string fileName = htmlFile.Filename();
            string outputName = fileName.Equals($"{Files.Index}{FileExtensions.Html}", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : fileName;

            await ProcessSingleHtmlFileAsync(htmlFile, pageName, outputName, appTemplateHtml, diagnostics);
        }
    }

    private async Task ProcessSingleHtmlFileAsync(string sourceFile, string pageName, string outputFileName, string appTemplateHtml, DiagnosticCollection diagnostics)
    {
        string fragment = await File.ReadAllTextAsync(sourceFile);
        // Diagnostics: warn on missing <head> or <main> fragments
        if (!HtmlTransformer.HasHeadSection(fragment))
        {
            _logger.LogError("Page fragment missing <head> section: {SourceFile}", sourceFile);
            diagnostics.Add(Diagnostic.Error("Page fragment missing <head> section", sourceFile));
        }
        if (!HtmlTransformer.HasMainSection(fragment))
        {
            _logger.LogError("Page fragment missing <main> section: {SourceFile}", sourceFile);
            diagnostics.Add(Diagnostic.Error("Page fragment missing <main> section", sourceFile));
        }
        string mergedHtml = HtmlTransformer.MergeTemplates(appTemplateHtml, fragment);

        string outputDir = workspace.FrontendBuildPath
            .Combine(Folders.Pages, pageName);
        outputDir.Create();

        string outputPath = outputDir.Combine(outputFileName);
        await File.WriteAllTextAsync(outputPath, mergedHtml);
    }

    private async Task PublishPageHtmlAsync(DiagnosticCollection? diagnostics)
    {
        string pagesPath = workspace.FrontendBuildPath.Combine(Folders.Pages);
        if (!pagesPath.Exists())
        {
            return;
        }

        foreach (string pageDir in pagesPath.Folders())
        {
            string pageName = pageDir.Filename();
            string pageHtml = pageDir.Combine($"{Files.Index}{FileExtensions.Html}");

            if (!pageHtml.Exists())
            {
                diagnostics?.Add(new Diagnostic { Level = DiagnosticLevel.Warning, Message = $"Missing page HTML: {pageHtml}", File = pageHtml });
                continue;
            }

            await PublishHtmlFileAsync(pageHtml, pageName);
        }
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
