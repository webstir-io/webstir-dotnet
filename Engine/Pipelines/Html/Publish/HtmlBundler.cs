using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Html.Constants;
using System.Text.RegularExpressions;

namespace Engine.Pipelines.Html.Publish;

public class HtmlBundler(AppWorkspace workspace)
{
    
    public async Task BundleAsync(DiagnosticCollection? diagnostics = null) => await BundlePageHtmlAsync(diagnostics);
    
    private async Task BundlePageHtmlAsync(DiagnosticCollection? diagnostics)
    {
        string pagesPath = workspace.ClientBuildPath.Combine(Folders.Pages);
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
            
            await ProcessHtmlFileAsync(pageHtml, pageName, diagnostics);
        }
    }
    
    private async Task ProcessHtmlFileAsync(string sourceFile, string pageName, DiagnosticCollection? diagnostics)
    {
        string htmlContent = await File.ReadAllTextAsync(sourceFile);
        
        htmlContent = HtmlRegex.RefreshScript().Replace(htmlContent, string.Empty);
        htmlContent = HtmlRegex.Comment().Replace(htmlContent, string.Empty);
        htmlContent = MinifyHtml(htmlContent);
        
        string distPagePath = workspace.ClientDistPath.Combine(Folders.Pages, pageName, $"{Files.Index}{FileExtensions.Html}");
        distPagePath.DirectoryName().Create();
        
        await File.WriteAllTextAsync(distPagePath, htmlContent);
    }
    
    
    private static string MinifyHtml(string html)
    {
        // Be safe: only collapse whitespace between tags; avoid touching content
        // inside <script>, <style>, <pre>, <textarea>
        html = Regex.Replace(html, @">\s+<", "><");
        return html.Trim();
    }
}
