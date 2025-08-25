using Engine.Extensions;
using Engine.Models;

namespace Engine.Handlers;

public class MarkupHandler(AppWorkspace workspace)
{
    private const string AppHtmlFileName = "app.html";
    private const string IndexHtmlFileName = "index.html";

    public async Task BuildAsync()
    {
        string appHtmlFilepath =  workspace.ClientAppPath.Combine(AppHtmlFileName);
        if (!File.Exists(appHtmlFilepath))
            throw new FileNotFoundException($"Base application HTML file not found: {appHtmlFilepath}");

        HtmlFile appHtmlFile = new(appHtmlFilepath);

        foreach (string page in workspace.ClientPagesPath.Folders())
            await ProcessPageDirectoryAsync(page, appHtmlFile);
    }

    public async Task PublishAsync()
    {
        foreach (string page in workspace.ClientBuildPath.Folders())
            await ProcessPublishPageAsync(page);
    }

    private async Task ProcessPublishPageAsync(string pageDirectory)
    {
        string? htmlFile = pageDirectory.Files(IndexHtmlFileName).SingleOrDefault();
        if (htmlFile != null)
        {
            string distPageDirectory = workspace.ClientDistPath.CreateSubDirectory(pageDirectory.Filename());
            string destinationPath = distPageDirectory.Combine(htmlFile);
            PublishHtmlFile(htmlFile, destinationPath);
        }
        
        await Task.CompletedTask;
    }

    public async Task AddPageAsync(string pageName)
    {
        string pagePath = workspace.ClientPagesPath.CreateSubDirectory(pageName);
        string htmlContent = GeneratePageTemplate(pageName);
        string outputPath = pagePath.Combine(IndexHtmlFileName);
        File.WriteAllText(outputPath, htmlContent);
        await Task.CompletedTask;
    }

    private async Task ProcessPageDirectoryAsync(string pagePath, HtmlFile appTemplate)
    {
        foreach (string pageHtmlFile in pagePath.Files(AddHtmlExt("*")))
            await ProcessPageFileAsync(pageHtmlFile, pagePath.Filename(), appTemplate);
    }

    private async Task ProcessPageFileAsync(string pageHtmlFile, string pageName, HtmlFile appTemplate)
    {
        if (!pageHtmlFile.Exists())
            throw new FileNotFoundException($"Page HTML file not found: {pageHtmlFile}");

        HtmlFile pageFragment = new(pageHtmlFile);
        string mergedHtml = appTemplate.Merge(pageFragment.Html);

        string outputPath = workspace.ClientBuildPath
            .CreateSubDirectory(Folders.Pages)
            .CreateSubDirectory(pageName)
            .Combine(IndexHtmlFileName);
            
        await File.WriteAllTextAsync(outputPath, mergedHtml);
    }

    private static void PublishHtmlFile(string sourceFilepath, string destinationPath)
    {
        string htmlContent = File.ReadAllText(sourceFilepath);
        HtmlFile htmlFile = new(htmlContent);    
        htmlFile.Remove(@"<script src=""/refresh.js"" async></script>");
        
        string cleanedContent = RemoveHtmlComments(htmlFile.Html);
        File.WriteAllText(destinationPath, cleanedContent);
    }

    private static string GeneratePageTemplate(string pageName)
    {
        return $"""
        <head>
            <title>{pageName}</title>
            <link rel="stylesheet" href="{Files.Index}.css" />
        </head>
        <body>
            <main>
                <h1>{pageName}</h1>
                <p>Content for the {pageName} page.</p>
            </main>
            <script type="module" src="{Files.Index}.js" async></script>
        </body>
        """;
    }



    private static string RemoveHtmlComments(string html)
    {
        return html;
    }
    
    private static string AddHtmlExt(string filename) => $"{filename}.html";

}