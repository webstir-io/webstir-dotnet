using CLI.Helpers;
using CLI.Interfaces;
using CLI.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CLI.Workers.Client;

public class MarkupWorker : IPageWorker
{
    private const string AppHtmlFileName = "app.html";
    private const string IndexHtmlFileName = "index.html";    
    
    private RoutingMetadata _routingMetadata = new();

    public int BuildOrder { get; } = 2;

    public void Init(ProjectMode mode = ProjectMode.Fullstack)
    {
        if (mode == ProjectMode.ServerOnly)
            return;
            
        InitializeTemplateFile(Directories.ClientAppDirectory, AppHtmlFileName);
        InitializeTemplateFile(Directories.ClientIndexDirectory, IndexHtmlFileName);
    }

    public void Build(bool releaseMode = false)
    {
        _routingMetadata = DetectRoutingConfiguration();
        var appTemplate = LoadAppTemplate();
        
        if (releaseMode)
            appTemplate.Remove(@"<script src=""/refresh.js"" async></script>");

        foreach (var pageDirectory in Directories.ClientPagesDirectory.GetDirectories())
            ProcessPageDirectory(pageDirectory, appTemplate, releaseMode);
    }

    public void Publish()
    {
        // Publish root index.html
        var rootIndexFile = Directories.ClientBuildDirectory.GetFiles(IndexHtmlFileName).FirstOrDefault();
        if (rootIndexFile != null)
            PublishHtmlFile(rootIndexFile, Directories.ClientDistDirectory.Join(IndexHtmlFileName));
        
        // Publish all page directories
        foreach (var pageDirectory in Directories.ClientBuildDirectory.GetDirectories())
        {
            var htmlFile = pageDirectory.GetFiles(IndexHtmlFileName).FirstOrDefault();
            if (htmlFile != null)
            {
                var distPageDirectory = Directories.ClientDistDirectory.SubDirectory(pageDirectory.Name);
                var destinationPath = distPageDirectory.Join(IndexHtmlFileName);
                PublishHtmlFile(htmlFile, destinationPath);
            }
        }
    }

    public void AddPage(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        var htmlContent = GeneratePageTemplate(pageName);
        var outputPath = pageDirectory.Join(IndexHtmlFileName);
        
        File.WriteAllText(outputPath, htmlContent);
        Console.WriteLine($"MarkupWorker: Created HTML fragment for page '{pageName}' at {outputPath}");
    }

    private static void InitializeTemplateFile(DirectoryInfo directory, string fileName)
    {
        var filePath = directory.Join(fileName);
        if (!File.Exists(filePath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, fileName, filePath);
    }

    private static HtmlFile LoadAppTemplate()
    {
        var appHtmlFilepath = Directories.ClientAppDirectory.Join(AppHtmlFileName);
        if (!File.Exists(appHtmlFilepath))
            throw new FileNotFoundException($"Base application HTML file not found: {appHtmlFilepath}");

        return new HtmlFile(appHtmlFilepath);
    }


    private void ProcessPageDirectory(DirectoryInfo pageDirectory, HtmlFile appTemplate, bool releaseMode)
    {
        foreach (var pageHtmlFile in pageDirectory.GetFiles("*.html"))
        {
            try
            {
                ProcessPageFile(pageHtmlFile, pageDirectory.Name, appTemplate, releaseMode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {pageHtmlFile.Name}: {ex.Message}");
                throw;
            }
        }
    }

    private void ProcessPageFile(FileInfo pageHtmlFile, string pageName, HtmlFile appTemplate, bool releaseMode)
    {
        var pageFragment = new HtmlFile(pageHtmlFile.FullName);
        var mergedHtml = appTemplate.Merge(pageFragment.Html);
        
        if (_routingMetadata.HasSpaPages && !releaseMode)
            mergedHtml = InjectRoutingMetadata(mergedHtml);
        
        string outputPath;
        if (pageName.Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            outputPath = Directories.ClientBuildDirectory.Join(IndexHtmlFileName);
        }
        else
        {
            var pageOutputDirectory = Directories.ClientBuildDirectory.SubDirectory(pageName);
            outputPath = pageOutputDirectory.Join(IndexHtmlFileName);
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

    private static RoutingMetadata DetectRoutingConfiguration()
    {
        var metadata = new RoutingMetadata();
        
        foreach (var pageDirectory in Directories.ClientPagesDirectory.GetDirectories())
        {
            AnalyzePageForRouting(pageDirectory, metadata);
        }
        
        // Check for router.ts in app directory
        var routerPath = Path.Combine(Directories.ClientAppDirectory.FullName, "router.ts");
        if (File.Exists(routerPath))
        {
            Console.WriteLine("Detected router.ts - SPA mode enabled globally");
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