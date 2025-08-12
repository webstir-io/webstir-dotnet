using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Engine.Workers.Client;

public class HtmlWorker(AppContext context) : IClientWorker
{
    private const string AppHtmlFileName = "app.html";
    private const string HomeHtmlFileName = "home.html";
    private const string IndexHtmlFileName = "index.html";

    private RoutingMetadata _routingMetadata = new();

    public int BuildOrder => 3;

    public async Task Init(ProjectMode mode = ProjectMode.Fullstack)
    {
        InitializeTemplateFile(context.ClientAppPath, AppHtmlFileName);
        InitializeTemplateFile(context.ClientPagesPath, HomeHtmlFileName);

        await Task.CompletedTask;
    }

    public async Task Build(bool releaseMode = false)
    {
        _routingMetadata = DetectRoutingConfiguration();
        var appTemplate = LoadAppTemplate();

        if (releaseMode)
            appTemplate.Remove(@"<script src=""/refresh.js"" async></script>");

        foreach (var page in context.ClientPagesPath.Folders())
            ProcessPageDirectory(page, appTemplate);

        await Task.CompletedTask;
    }

    public async Task Publish()
    {
        foreach (var page in context.ClientBuildPath.Folders())
        {
            string? htmlFile = page.Files(IndexHtmlFileName).SingleOrDefault();
            if (htmlFile != null)
            {
                var distPageDirectory = context.ClientDistPath.CreateSubDirectory(page.Name());
                var destinationPath = distPageDirectory.Combine(HomeHtmlFileName);
                PublishHtmlFile(htmlFile, destinationPath);
            }
        }

        await Task.CompletedTask;
    }

    public async Task AddPage(DirectoryInfo pageDirectory)
    {
        if (!pageDirectory.Exists)
            throw new DirectoryNotFoundException($"Page directory '{pageDirectory.Name}' does not exist.");

        // Create index.html for the new page
        CreatePageTemplate(pageDirectory);

        await Task.CompletedTask;
    }

    public async Task AddPage(string pageName)
    {
        var pageDirectory = context.ClientPagesPath.CreateSubDirectory(pageName);
        await AddPage(pageDirectory);
    }

    private static void CreatePageTemplate(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        var htmlContent = GeneratePageTemplate(pageName);
        var outputPath = pageDirectory.CombinePath(HomeHtmlFileName);

        File.WriteAllText(outputPath, htmlContent);
        Console.WriteLine($"MarkupWorker: Created HTML fragment for page '{pageName}' at {outputPath}");
    }

    private static void InitializeTemplateFile(string path, string fileName)
    {
        var filePath = path.Combine(fileName);
        if (!File.Exists(filePath))
            AssemblyHelpers.WriteResourceToFile(Folders.Client, fileName, filePath);
    }

    private HtmlFile LoadAppTemplate()
    {
        var appHtmlFilepath = context.ClientAppPath.Combine(AppHtmlFileName);
        if (!File.Exists(appHtmlFilepath))
            throw new FileNotFoundException($"Base application HTML file not found: {appHtmlFilepath}");

        return new HtmlFile(appHtmlFilepath);
    }


    private void ProcessPageDirectory(string pagePath, HtmlFile appTemplate)
    {
        foreach (var pageHtmlFile in pagePath.Files(AddHtmlExt("*")))
        {
            try
            {
                ProcessPageFile(pageHtmlFile, pagePath.Name(), appTemplate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {pageHtmlFile.Name}: {ex.Message}");
                throw;
            }
        }
    }

    private void ProcessPageFile(string pageHtmlFile, string pageName, HtmlFile appTemplate)
    {
        if (!pageHtmlFile.Exists())
            throw new FileNotFoundException($"Page HTML file not found: {pageHtmlFile}");

        // Merge the app template with the page fragment
        var releaseMode = _routingMetadata.Pages.ContainsKey(pageName) && _routingMetadata.Pages[pageName].IsSpaEnabled;
        if (releaseMode && !_routingMetadata.HasSpaPages)
            return; // Skip if SPA mode is not enabled for this page

        Console.WriteLine($"Processing page: {pageName}");

        var pageFragment = new HtmlFile(pageHtmlFile);
        var mergedHtml = appTemplate.Merge(pageFragment.Html);

        if (_routingMetadata.HasSpaPages && !releaseMode)
            mergedHtml = InjectRoutingMetadata(mergedHtml);

        string outputPath;
        if (pageName.Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            outputPath = context.ClientBuildPath.Combine(HomeHtmlFileName);
        }
        else
        {
            var pageOutputPath = context.ClientBuildPath.CreateSubDirectory(pageName);
            outputPath = pageOutputPath.Combine(HomeHtmlFileName);
        }

        File.WriteAllText(outputPath, mergedHtml);
    }

    private static void PublishHtmlFile(string sourceFilepath, string destinationPath)
    {
        var htmlContent = File.ReadAllText(sourceFilepath);
        var cleanedContent = RemoveHtmlComments(htmlContent);
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

    private RoutingMetadata DetectRoutingConfiguration()
    {
        var metadata = new RoutingMetadata();

        foreach (var page in context.ClientPagesPath.Folders())
        {
            AnalyzePageForRouting(page, metadata);
        }

        // Check for router.ts in app directory
        var routerPath = Path.Combine(context.ClientAppPath, "router.ts");
        if (File.Exists(routerPath))
        {
            // Router detected - SPA mode will be enabled
        }

        return metadata;
    }

    private static void AnalyzePageForRouting(string page, RoutingMetadata metadata)
    {
        var pageName = Path.GetFileName(page);
        var typeScriptFiles = Directory.GetFiles(page, "*.ts");

        foreach (var tsFile in typeScriptFiles)
        {
            var content = File.ReadAllText(tsFile);
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