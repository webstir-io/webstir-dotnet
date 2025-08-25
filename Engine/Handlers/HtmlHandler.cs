using Engine.Extensions;
using Engine.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Engine.Handlers;

public partial class HtmlHandler(
    AppWorkspace workspace,
    JsonSerializerOptions jsonOptions)
{
    private const string AppHtmlFileName = "app.html";
    private const string IndexHtmlFileName = "index.html";

    private RoutingMetadata _routingMetadata = new();

    public async Task BuildAsync()
    {
        _routingMetadata = await DetectRoutingConfigurationAsync();
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

        bool releaseMode = _routingMetadata.Pages.ContainsKey(pageName) && _routingMetadata.Pages[pageName].IsSpaEnabled;
        if (releaseMode && !_routingMetadata.HasSpaPages)
            return;

        HtmlFile pageFragment = new(pageHtmlFile);
        string mergedHtml = appTemplate.Merge(pageFragment.Html);

        if (_routingMetadata.HasSpaPages && !releaseMode)
            mergedHtml = InjectRoutingMetadata(mergedHtml);

        mergedHtml = UpdateAssetReferences(mergedHtml);

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

    private string GeneratePageTemplate(string pageName)
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

    private async Task<RoutingMetadata> DetectRoutingConfigurationAsync()
    {
        RoutingMetadata metadata = new();

        foreach (string page in workspace.ClientPagesPath.Folders())
            await AnalyzePageForRoutingAsync(page, metadata);

        string routerPath = Path.Combine(workspace.ClientAppPath, "router.ts");
        if (File.Exists(routerPath))
            metadata.HasGlobalRouter = true;

        return metadata;
    }

    private static async Task AnalyzePageForRoutingAsync(string page, RoutingMetadata metadata)
    {
        string pageName = Path.GetFileName(page);
        string[] typeScriptFiles = Directory.GetFiles(page, "*.ts");

        foreach (string tsFile in typeScriptFiles)
        {
            string content = await File.ReadAllTextAsync(tsFile);
            bool hasRouteHandler = DetectRouteHandlerExport(content);

            metadata.Pages[pageName] = new PageRouteInfo
            {
                PageName = pageName,
                Route = $"/{pageName}",
                IsSpaEnabled = hasRouteHandler,
                TypeScriptPath = tsFile
            };
        }
    }

    private static bool DetectRouteHandlerExport(string typeScriptContent)
    {
        return RouteHandlerExportRegex().IsMatch(typeScriptContent) ||
               RouteHandlerNamedExportRegex().IsMatch(typeScriptContent) ||
               RouteHandlerDefaultExportRegex().IsMatch(typeScriptContent);
    }

    private string InjectRoutingMetadata(string html)
    {
        string metadataJson = JsonSerializer.Serialize(_routingMetadata, jsonOptions);

        string metadataScript = $"""
            <script id="app-routing-metadata" type="application/json">
            {metadataJson}
            </script>
            """;

        int bodyCloseIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyCloseIndex > -1
            ? html.Insert(bodyCloseIndex, metadataScript)
            : html;
    }

    private static string RemoveHtmlComments(string html)
    {
        string withoutLineComments = CommentLineRegex().Replace(html, string.Empty);
        return InlineCommentRegex().Replace(withoutLineComments, string.Empty);
    }
    
    private static string AddHtmlExt(string filename) => $"{filename}.html";

    private string UpdateAssetReferences(string html)
    {
        return html;
    }


    [GeneratedRegex(@"^\s*<!--[\s\S]*?-->\s*\r?\n", RegexOptions.Multiline)]
    private static partial Regex CommentLineRegex();

    [GeneratedRegex(@"<!--[\s\S]*?-->")]
    private static partial Regex InlineCommentRegex();

    [GeneratedRegex(@"export\s+(const|let|var)\s+routeHandler\s*=", RegexOptions.Multiline)]
    private static partial Regex RouteHandlerExportRegex();

    [GeneratedRegex(@"export\s+{[^}]*\brouteHandler\b[^}]*}", RegexOptions.Multiline)]
    private static partial Regex RouteHandlerNamedExportRegex();

    [GeneratedRegex(@"export\s+default\s+{[^}]*\brouteHandler\b[^}]*}", RegexOptions.Multiline)]
    private static partial Regex RouteHandlerDefaultExportRegex();
}