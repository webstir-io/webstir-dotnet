using Engine.Extensions;
using Engine.Models;

namespace Engine.Pipelines.Html.Build;

public class HtmlBuilder(AppWorkspace workspace)
{
    private const string AppHtmlFileName = "app.html";
    
    public async Task BuildAsync()
    {
        string appHtmlPath = workspace.ClientAppPath.Combine(AppHtmlFileName);
        if (!appHtmlPath.Exists())
            throw new FileNotFoundException($"Base application HTML file not found: {appHtmlPath}");

        HtmlFile appTemplate = new(appHtmlPath);
        
        await BuildPageHtmlFilesAsync(appTemplate);
    }
    
    private async Task BuildPageHtmlFilesAsync(HtmlFile appTemplate)
    {
        string pagesPath = workspace.ClientPagesPath;
        if (!pagesPath.Exists())
            return;
        
        foreach (string pageDir in pagesPath.Folders())
        {
            string pageName = pageDir.Filename();
            await ProcessPageHtmlFilesAsync(pageDir, pageName, appTemplate);
        }
    }
    
    private async Task ProcessPageHtmlFilesAsync(string pageDir, string pageName, HtmlFile appTemplate)
    {
        string[] htmlFiles = pageDir.Files($"*{FileExtensions.Html}");
        
        foreach (string htmlFile in htmlFiles)
        {
            string fileName = htmlFile.Filename();
            string outputName = fileName.Equals($"{Files.Index}{FileExtensions.Html}", StringComparison.OrdinalIgnoreCase) 
                ? fileName 
                : fileName;
            
            await ProcessSingleHtmlFileAsync(htmlFile, pageName, outputName, appTemplate);
        }
    }
    
    private async Task ProcessSingleHtmlFileAsync(string sourceFile, string pageName, string outputFileName, HtmlFile appTemplate)
    {
        HtmlFile pageFragment = new(sourceFile);
        string mergedHtml = appTemplate.Merge(pageFragment.Html);
        
        string outputDir = workspace.ClientBuildPath
            .Combine(Folders.Pages, pageName);
        outputDir.Create();
        
        string outputPath = outputDir.Combine(outputFileName);
        await File.WriteAllTextAsync(outputPath, mergedHtml);
    }
}