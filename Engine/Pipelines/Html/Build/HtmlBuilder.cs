using Engine.Extensions;
using Engine.Models;
using Engine.Pipelines.Core;

using Microsoft.Extensions.Logging;
using Engine.Pipelines.Html.Constants;

namespace Engine.Pipelines.Html.Build;

public class HtmlBuilder(AppWorkspace workspace, ILogger<HtmlBuilder> logger)
{
    private const string AppHtmlFileName = "app.html";
    private readonly ILogger<HtmlBuilder> _logger = logger;

    public async Task BuildAsync(DiagnosticCollection? diagnostics = null)
    {
        DiagnosticCollection diag = diagnostics ?? new DiagnosticCollection();
        string appHtmlPath = workspace.ClientAppPath.Combine(AppHtmlFileName);
        if (!appHtmlPath.Exists())
        {
            _logger.LogError("Base application HTML file not found: {AppHtmlPath}", appHtmlPath);
            diag.Add(Diagnostic.Error($"Base application HTML file not found: {appHtmlPath}", appHtmlPath));
            return;
        }

        HtmlFile appTemplate = new(appHtmlPath);

        // Validate template structure: require a <main> container to merge into
        if (!HtmlRegex.MainContent().IsMatch(appTemplate.Html))
        {
            _logger.LogError("Base template missing <main> container: {AppHtmlPath}", appHtmlPath);
            diag.Add(Diagnostic.Error("Base template missing <main> container", appHtmlPath));
        }

        await BuildPageHtmlFilesAsync(appTemplate, diag);
    }

    private async Task BuildPageHtmlFilesAsync(HtmlFile appTemplate, DiagnosticCollection diagnostics)
    {
        string pagesPath = workspace.ClientPagesPath;
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
        if (!HtmlRegex.HeadContent().IsMatch(fragment))
        {
            _logger.LogWarning("Page fragment missing <head> section: {SourceFile}", sourceFile);
            diagnostics.Add(new Diagnostic { Level = DiagnosticLevel.Warning, Message = "Page fragment missing <head> section", File = sourceFile });
        }
        if (!HtmlRegex.MainContent().IsMatch(fragment))
        {
            _logger.LogWarning("Page fragment missing <main> section: {SourceFile}", sourceFile);
            diagnostics.Add(new Diagnostic { Level = DiagnosticLevel.Warning, Message = "Page fragment missing <main> section", File = sourceFile });
        }
        string mergedHtml = appTemplate.Merge(pageFragment.Html);

        string outputDir = workspace.ClientBuildPath
            .Combine(Folders.Pages, pageName);
        outputDir.Create();

        string outputPath = outputDir.Combine(outputFileName);
        await File.WriteAllTextAsync(outputPath, mergedHtml);
    }
}
