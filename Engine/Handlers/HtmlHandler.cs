using Engine.Extensions;
using Engine.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Engine.Handlers;

public class HtmlHandler(AppContext context)
{
    private const string AppHtmlFileName = "app.html";
    private const string IndexHtmlFileName = "index.html";

    private RoutingMetadata _routingMetadata = new();

    public async Task BuildAsync()
    {
        _routingMetadata = await DetectRoutingConfigurationAsync();
        string appHtmlFilepath =  context.ClientAppPath.Combine(AppHtmlFileName);
        if (!File.Exists(appHtmlFilepath))
            throw new FileNotFoundException($"Base application HTML file not found: {appHtmlFilepath}");

        var appHtmlFile = new HtmlFile(appHtmlFilepath);

        foreach (var page in context.ClientPagesPath.Folders())
            await ProcessPageDirectoryAsync(page, appHtmlFile);
    }

    public async Task PublishAsync()
    {
        foreach (var page in context.ClientBuildPath.Folders())
        {
            string? htmlFile = page.Files(IndexHtmlFileName).SingleOrDefault();
            if (htmlFile != null)
            {
                var distPageDirectory = context.ClientDistPath.CreateSubDirectory(page.Name());
                var destinationPath = distPageDirectory.Combine(htmlFile);
                PublishHtmlFile(htmlFile, destinationPath);
            }
        }

        await Task.CompletedTask;
    }

    public async Task AddPageAsync(string pageName)
    {
        var pagePath = context.ClientPagesPath.CreateSubDirectory(pageName);
        var htmlContent = GeneratePageTemplate(pageName);
        var outputPath = pagePath.Combine(IndexHtmlFileName);
        File.WriteAllText(outputPath, htmlContent);
        await Task.CompletedTask;
    }


    private async Task ProcessPageDirectoryAsync(string pagePath, HtmlFile appTemplate)
    {
        foreach (var pageHtmlFile in pagePath.Files(AddHtmlExt("*")))
        {
            try
            {
                await ProcessPageFileAsync(pageHtmlFile, pagePath.Name(), appTemplate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {pageHtmlFile.Name}: {ex.Message}");
                throw;
            }
        }
    }

    private async Task ProcessPageFileAsync(string pageHtmlFile, string pageName, HtmlFile appTemplate)
    {
        if (!pageHtmlFile.Exists())
            throw new FileNotFoundException($"Page HTML file not found: {pageHtmlFile}");

        // Merge the app template with the page fragment
        bool releaseMode = _routingMetadata.Pages.ContainsKey(pageName) && _routingMetadata.Pages[pageName].IsSpaEnabled;
        if (releaseMode && !_routingMetadata.HasSpaPages)
            return;

        HtmlFile pageFragment = new(pageHtmlFile);
        string mergedHtml = appTemplate.Merge(pageFragment.Html);

        if (_routingMetadata.HasSpaPages && !releaseMode)
            mergedHtml = InjectRoutingMetadata(mergedHtml);

        string outputPath;
        string pagesDirectory = context.ClientBuildPath.CreateSubDirectory("pages");
        string pageOutputPath = pagesDirectory.CreateSubDirectory(pageName);
        outputPath = pageOutputPath.Combine(IndexHtmlFileName);
        await File.WriteAllTextAsync(outputPath, mergedHtml);
    }

    private static void PublishHtmlFile(string sourceFilepath, string destinationPath)
    {
        var htmlContent = File.ReadAllText(sourceFilepath);
        var htmlFile = new HtmlFile(htmlContent);
        
        // Remove development artifacts for production
        htmlFile.Remove(@"<script src=""/refresh.js"" async></script>");
        
        var cleanedContent = RemoveHtmlComments(htmlFile.Html);
        File.WriteAllText(destinationPath, cleanedContent);
    }

    private static string GeneratePageTemplate(string pageName) =>
        $"""
        <head>
            <title>{pageName}</title>
            <link rel="stylesheet" href="{pageName}.css" />
            <script type="module" src="{pageName}.js" async></script>
        </head>
        <body>
            <main>
                <h1>{pageName}</h1>
                <p>Content for the {pageName} page.</p>
            </main>
        </body>
        """;

    private async Task<RoutingMetadata> DetectRoutingConfigurationAsync()
    {
        var metadata = new RoutingMetadata();

        foreach (var page in context.ClientPagesPath.Folders())
        {
            await AnalyzePageForRoutingAsync(page, metadata);
        }

        // Check for router.ts in app directory
        var routerPath = Path.Combine(context.ClientAppPath, "router.ts");
        if (File.Exists(routerPath))
        {
            // Router detected - SPA mode will be enabled
        }

        return metadata;
    }

    private static async Task AnalyzePageForRoutingAsync(string page, RoutingMetadata metadata)
    {
        var pageName = Path.GetFileName(page);
        var typeScriptFiles = Directory.GetFiles(page, "*.ts");

        foreach (var tsFile in typeScriptFiles)
        {
            var content = await File.ReadAllTextAsync(tsFile);
            var hasRouteHandler = DetectRouteHandlerExport(content);

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
        var routeHandlerPatterns = new[]
        {
            @"export\s+(const|let|var)\s+routeHandler\s*=",
            @"export\s+{[^}]*\brouteHandler\b[^}]*}",
            @"export\s+default\s+{[^}]*\brouteHandler\b[^}]*}"
        };

        return routeHandlerPatterns.Any(pattern =>
            Regex.IsMatch(typeScriptContent, pattern, RegexOptions.Multiline));
    }

    private string InjectRoutingMetadata(string html)
    {
        var metadataJson = JsonSerializer.Serialize(_routingMetadata, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        var metadataScript = $"""
            <script id="app-routing-metadata" type="application/json">
            {metadataJson}
            </script>
            """;

        var bodyCloseIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyCloseIndex > -1
            ? html.Insert(bodyCloseIndex, metadataScript)
            : html;
    }

    private static string RemoveHtmlComments(string html)
    {
        const string commentLinePattern = @"^\s*<!--[\s\S]*?-->\s*\r?\n";
        const string inlineCommentPattern = @"<!--[\s\S]*?-->";

        var withoutLineComments = Regex.Replace(
            html,
            commentLinePattern,
            string.Empty,
            RegexOptions.Multiline
        );

        return Regex.Replace(withoutLineComments, inlineCommentPattern, string.Empty);
    }
    
    private static string AddHtmlExt(string filename)
    {
        return $"{filename}.html";
    }
}