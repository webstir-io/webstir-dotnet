using CLI.Helpers;
using CLI.Interfaces;
using CLI.Models;

namespace CLI.Workers;

public class MarkupWorker : IFileWorker
{
    private const string _appHtmlFile = "app.html";
    private const string _indexHtmlFile = "index.html";

    public int BuildOrder { get; } = 2;

    public void Init()
    {
        var appFilepath = Directories.ClientAppDirectory.Join(_appHtmlFile);
        if (!File.Exists(appFilepath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _appHtmlFile, appFilepath);
        
        var indexFilepath = Directories.ClientIndexDirectory.Join(_indexHtmlFile);
        if (!File.Exists(indexFilepath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _indexHtmlFile, indexFilepath);
    }

    public void Build(bool releaseMode = false)
    {
        // Find the base app.html file
        var appHtmlFilepath = Directories.ClientAppDirectory.Join(_appHtmlFile);
        if (!File.Exists(appHtmlFilepath))
            throw new FileNotFoundException($"Base application HTML file not found: {appHtmlFilepath}");

        var appHtmlFile = new HtmlFile(appHtmlFilepath);
        if (releaseMode)
        {
            // Remove refresh.js script tag in release mode
            appHtmlFile.Remove(@"<script src=""refresh.js"" async></script>");
        }

        Directory.CreateDirectory(Directories.BuildDirectory.FullName); // Ensure build directory exists

        foreach (var pageSourceDirectory in Directories.ClientPagesDirectory.GetDirectories())
        {
            foreach (var pageHtmlFragmentFile in pageSourceDirectory.GetFiles("*.html"))
            {
                try
                {
                    var pageFragment = new HtmlFile(pageHtmlFragmentFile.FullName);
                    string mergedHtmlContent = appHtmlFile.Merge(pageFragment.Html);

                    string outputFilePath = Path.Combine(Directories.ClientBuildDirectory.FullName, pageHtmlFragmentFile.Name);

                    File.WriteAllText(outputFilePath, mergedHtmlContent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {pageHtmlFragmentFile.Name}: {ex.Message}");
                    throw;
                }
            }
        }
    }

    public void Publish()
    {
        Directory.CreateDirectory(Directories.ClientDistDirectory.FullName);

        foreach (var htmlFileToPublish in Directories.ClientBuildDirectory.GetFiles("*.html", SearchOption.TopDirectoryOnly))
        {
            string htmlContent = File.ReadAllText(htmlFileToPublish.FullName);
            
            // Remove HTML comments for production
            htmlContent = RemoveHtmlComments(htmlContent);
            
            string destinationFilePath = Path.Combine(Directories.ClientDistDirectory.FullName, htmlFileToPublish.Name);
            File.WriteAllText(destinationFilePath, htmlContent);
        }
    }

    public void Add(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;

        var baseHtmlFragment =
$@"<head>
    <title>{pageName}</title>
    <link rel=""stylesheet"" href=""pages/{pageName}/{pageName}.css"" />
    <script type=""module"" src=""pages/{pageName}/{pageName}.js"" async></script>
</head>
<body>
    <main>
        <h1>{pageName}</h1>
        <p>Content for the {pageName} page.</p>
    </main>
</body>";

        string newPageFragmentPath = Path.Combine(pageDirectory.FullName, $"{pageName}.html");
        File.WriteAllText(newPageFragmentPath, baseHtmlFragment);
        Console.WriteLine($"MarkupWorker: Created HTML fragment for page '{pageName}' at {newPageFragmentPath}");
    }

    private static string RemoveHtmlComments(string html)
    {
        // Remove entire lines containing only comments (including the newline)
        var commentLinePattern = @"^\s*<!--[\s\S]*?-->\s*\r?\n";
        var result = System.Text.RegularExpressions.Regex.Replace(
            html, 
            commentLinePattern, 
            string.Empty, 
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        
        // Also remove inline comments (comments not on their own line)
        var inlineCommentPattern = @"<!--[\s\S]*?-->";
        result = System.Text.RegularExpressions.Regex.Replace(result, inlineCommentPattern, string.Empty);
        
        return result;
    }
}