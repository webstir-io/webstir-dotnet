using System;
using System.IO;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Html.Models;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Utilities;

using Microsoft.Extensions.Logging;
using Engine.Pipelines.Html.Common;
using Engine.Pipelines.Html.Parsing;
using Engine.Pipelines.Html.Transformation;

namespace Engine.Pipelines.Html.Build;

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

        HtmlFile appTemplate = new(appHtmlPath);

        // Validate template structure: require a <main> container to merge into
        if (!HtmlParser.HasMainSection(appTemplate.Html))
        {
            _logger.LogError("Base template missing <main> container: {AppHtmlPath}", appHtmlPath);
            diag.Add(Diagnostic.Error("Base template missing <main> container", appHtmlPath));
        }

        await BuildPageHtmlFilesAsync(appTemplate, diag);

        if (diag.HasErrors)
        {
            throw new InvalidOperationException("HTML build failed due to errors in page fragments or base template.");
        }
    }

    private async Task BuildPageHtmlFilesAsync(HtmlFile appTemplate, DiagnosticCollection diagnostics)
    {
        string pagesPath = workspace.FrontendPagesPath;
        if (!pagesPath.Exists())
            return;

        foreach (string pageDir in pagesPath.Folders())
        {
            string pageName = pageDir.Filename();
            await ProcessPageHtmlFilesAsync(pageDir, pageName, appTemplate, diagnostics);
        }
    }

    private async Task ProcessPageHtmlFilesAsync(string pageDir, string pageName, HtmlFile appTemplate, DiagnosticCollection diagnostics)
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

            await ProcessSingleHtmlFileAsync(htmlFile, pageName, outputName, appTemplate, diagnostics);
        }
    }

    private async Task ProcessSingleHtmlFileAsync(string sourceFile, string pageName, string outputFileName, HtmlFile appTemplate, DiagnosticCollection diagnostics)
    {
        HtmlFile pageFragment = new(sourceFile);
        // Diagnostics: warn on missing <head> or <main> fragments
        string fragment = pageFragment.Html;
        if (!HtmlParser.HasHeadSection(fragment))
        {
            _logger.LogError("Page fragment missing <head> section: {SourceFile}", sourceFile);
            diagnostics.Add(Diagnostic.Error("Page fragment missing <head> section", sourceFile));
        }
        if (!HtmlParser.HasMainSection(fragment))
        {
            _logger.LogError("Page fragment missing <main> section: {SourceFile}", sourceFile);
            diagnostics.Add(Diagnostic.Error("Page fragment missing <main> section", sourceFile));
        }
        string mergedHtml = appTemplate.Merge(pageFragment.Html);

        string outputDir = workspace.FrontendBuildPath
            .Combine(Folders.Pages, pageName);
        outputDir.Create();

        string outputPath = outputDir.Combine(outputFileName);
        await File.WriteAllTextAsync(outputPath, mergedHtml);
    }
}
