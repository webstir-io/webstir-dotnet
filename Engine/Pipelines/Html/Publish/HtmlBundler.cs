using Engine.Extensions;
using Engine.Pipelines.Html.Constants;

namespace Engine.Pipelines.Html.Publish;

public class HtmlBundler(AppWorkspace workspace)
{
    
    public async Task BundleAsync()
    {
        await BundlePageHtmlAsync();
    }
    
    private async Task BundlePageHtmlAsync()
    {
        string pagesPath = workspace.ClientBuildPath.Combine(Folders.Pages);
        if (!pagesPath.Exists())
            return;
        
        foreach (string pageDir in pagesPath.Folders())
        {
            string pageName = pageDir.Filename();
            string pageHtml = pageDir.Combine($"{Files.Index}{FileExtensions.Html}");
            
            if (!pageHtml.Exists())
                continue;
            
            await ProcessHtmlFileAsync(pageHtml, pageName);
        }
    }
    
    private async Task ProcessHtmlFileAsync(string sourceFile, string pageName)
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
        html = HtmlRegex.Whitespace().Replace(html, " ");
        html = html.Trim();

        return html;
    }
}