using Engine.Extensions;
using Engine.Helpers;
using Engine.Servers;
using Engine.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Engine.Workers.Client;

public class HtmlWorker(App app) : IClientWorker
{
    private const string AppHtmlFileName = "app.html";
    private const string IndexHtmlFileName = "index.html";    
    
    private RoutingMetadata _routingMetadata = new();

    public int BuildOrder => 3; // Fast operations can run together after TS compilation

    public void Init(ProjectMode mode = ProjectMode.Fullstack)
    {
        InitializeTemplateFile(app.ClientAppDir, AppHtmlFileName);
        InitializeTemplateFile(app.ClientIndexDir, IndexHtmlFileName);
    }

    public void Build(bool releaseMode = false)
    {
        _routingMetadata = DetectRoutingConfiguration();
        var appTemplate = LoadAppTemplate();
        
        if (releaseMode)
            appTemplate.Remove(@"<script src=""/refresh.js"" async></script>");

        foreach (var pageDirectory in app.ClientPagesDir.GetDirectories())
            ProcessPageDirectory(pageDirectory, appTemplate);
    }

    public void Publish()
    {
        // Publish root index.html
        var rootIndexFile = app.ClientBuildDir.GetFiles(IndexHtmlFileName).FirstOrDefault();
        if (rootIndexFile != null)
            PublishHtmlFile(rootIndexFile, app.ClientDistDir.CombinePath(IndexHtmlFileName));

        // Publish all page directories
        foreach (var pageDirectory in app.ClientBuildDir.GetDirectories())
        {
            var htmlFile = pageDirectory.GetFiles(IndexHtmlFileName).FirstOrDefault();
            if (htmlFile != null)
            {
                var distPageDirectory = app.ClientDistDir.CreateSubDirectory(pageDirectory.Name);
                var destinationPath = distPageDirectory.CombinePath(IndexHtmlFileName);
                PublishHtmlFile(htmlFile, destinationPath);
            }
        }
    }

    public void AddPage(DirectoryInfo pageDirectory)
    {
        if (!pageDirectory.Exists)
            throw new DirectoryNotFoundException($"Page directory '{pageDirectory.Name}' does not exist.");

        // Create index.html for the new page
        CreatePageTemplate(pageDirectory);
    }

    public void AddPage(string pageName)
    {
        var pageDirectory = app.ClientPagesDir.CreateSubDirectory(pageName);
        AddPage(pageDirectory);
    }

    private static void CreatePageTemplate(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        var htmlContent = GeneratePageTemplate(pageName);
        var outputPath = pageDirectory.CombinePath(IndexHtmlFileName);

        File.WriteAllText(outputPath, htmlContent);
        Console.WriteLine($"MarkupWorker: Created HTML fragment for page '{pageName}' at {outputPath}");
    }

    private static void InitializeTemplateFile(DirectoryInfo directory, string fileName)
    {
        var filePath = directory.CombinePath(fileName);
        if (!File.Exists(filePath))
            AssemblyHelpers.WriteResourceToFile(App.Folders.Client, fileName, filePath);
    }

    private HtmlFile LoadAppTemplate()
    {
        var appHtmlFilepath = app.ClientAppDir.CombinePath(AppHtmlFileName);
        if (!File.Exists(appHtmlFilepath))
            throw new FileNotFoundException($"Base application HTML file not found: {appHtmlFilepath}");

        return new HtmlFile(appHtmlFilepath);
    }


    private void ProcessPageDirectory(DirectoryInfo pageDirectory, HtmlFile appTemplate)
    {
        foreach (var pageHtmlFile in pageDirectory.GetFiles("*.html"))
        {
            try
            {
                ProcessPageFile(pageHtmlFile, pageDirectory.Name, appTemplate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {pageHtmlFile.Name}: {ex.Message}");
                throw;
            }
        }
    }

    private void ProcessPageFile(FileInfo pageHtmlFile, string pageName, HtmlFile appTemplate)
    {
        if (!pageHtmlFile.Exists)
            throw new FileNotFoundException($"Page HTML file not found: {pageHtmlFile.FullName}");

        // Merge the app template with the page fragment
        var releaseMode = _routingMetadata.Pages.ContainsKey(pageName) && _routingMetadata.Pages[pageName].IsSpaEnabled;
        if (releaseMode && !_routingMetadata.HasSpaPages)
            return; // Skip if SPA mode is not enabled for this page

        Console.WriteLine($"Processing page: {pageName}");
    
        var pageFragment = new HtmlFile(pageHtmlFile.FullName);
        var mergedHtml = appTemplate.Merge(pageFragment.Html);
        
        if (_routingMetadata.HasSpaPages && !releaseMode)
            mergedHtml = InjectRoutingMetadata(mergedHtml);
        
        string outputPath;
        if (pageName.Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            outputPath = app.ClientBuildDir.CombinePath(IndexHtmlFileName);
        }
        else
        {
            var pageOutputDirectory = app.ClientBuildDir.CreateSubDirectory(pageName);
            outputPath = pageOutputDirectory.CombinePath(IndexHtmlFileName);
        }
        
        File.WriteAllText(outputPath, mergedHtml);
    }

    private static void PublishHtmlFile(FileInfo sourceFile, string destinationPath)
    {
        var htmlContent = File.ReadAllText(sourceFile.FullName);
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

        foreach (var pageDirectory in app.ClientPagesDir.GetDirectories())
        {
            AnalyzePageForRouting(pageDirectory, metadata);
        }
        
        // Check for router.ts in app directory
        var routerPath = Path.Combine(app.ClientAppDir.FullName, "router.ts");
        if (File.Exists(routerPath))
        {
            // Router detected - SPA mode will be enabled
        }
        
        return metadata;
    }

    private static void AnalyzePageForRouting(DirectoryInfo pageDirectory, RoutingMetadata metadata)
    {
        var pageName = pageDirectory.Name;
        var typeScriptFiles = pageDirectory.GetFiles("*.ts");
        
        foreach (var tsFile in typeScriptFiles)
        {
            var content = File.ReadAllText(tsFile.FullName);
            var hasRouteHandler = DetectRouteHandlerExport(content);
            
            metadata.Pages[pageName] = new PageRouteInfo
            {
                PageName = pageName,
                Route = $"/{pageName}",
                IsSpaEnabled = hasRouteHandler,
                TypeScriptPath = tsFile.FullName
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
}