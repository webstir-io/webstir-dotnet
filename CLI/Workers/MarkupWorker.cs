using CLI.Helpers;
using CLI.Models;

namespace CLI.Workers;

public class MarkupWorker : IWebFileWorker
{
    private const string _appHtmlFile = "app.html";
    private const string _indexHtmlFile = "index.html";

    public int BuildOrder { get; } = 2;

    public void Init()
    {
        var appFilepath = Directories.AppDirectory.Join(_appHtmlFile);
        if (!File.Exists(appFilepath))
            AssemblyHelpers.WriteResourceToFile(_appHtmlFile, appFilepath);
        
        var indexFilepath = Directories.IndexDirectory.Join(_indexHtmlFile);
        if (!File.Exists(indexFilepath))
            AssemblyHelpers.WriteResourceToFile(_indexHtmlFile, indexFilepath);
    }

    public void Build(bool releaseMode = false)
    {
        // Find the base app.html file
        var appHtmlFilepath = Directories.AppDirectory.Join(_appHtmlFile); // e.g., src/app/app.html
        if (!File.Exists(appHtmlFilepath))
            throw new FileNotFoundException($"Base application HTML file not found: {appHtmlFilepath}");

        var appHtmlFile = new HtmlFile(appHtmlFilepath);
        if (releaseMode)
        {
            // Remove refresh.js script tag in release mode
            appHtmlFile.Remove(@"<script src=""refresh.js"" async></script>");
        }

        Directory.CreateDirectory(Directories.BuildDirectory.FullName); // Ensure build directory exists

        foreach (var pageSourceDirectory in Directories.PagesDirectory.GetDirectories()) // e.g., src/pages/index
        {
            foreach (var pageHtmlFragmentFile in pageSourceDirectory.GetFiles("*.html")) // e.g., src/pages/index/index.html
            {
                try
                {
                    var pageFragment = new HtmlFile(pageHtmlFragmentFile.FullName);
                    string mergedHtmlContent = appHtmlFile.Merge(pageFragment.Html);

                    // Output path will be like build/index.html or build/login.html
                    string outputFilePath = Path.Combine(Directories.BuildDirectory.FullName, pageHtmlFragmentFile.Name);

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
        Directory.CreateDirectory(Directories.DistDirectory.FullName); // Ensure dist directory exists

        // Copy all HTML files from the root of the build directory to the dist directory
        foreach (var htmlFileToPublish in Directories.BuildDirectory.GetFiles("*.html", SearchOption.TopDirectoryOnly))
        {
            string htmlContent = File.ReadAllText(htmlFileToPublish.FullName);
            
            // Remove HTML comments for production
            htmlContent = RemoveHtmlComments(htmlContent);
            
            string destinationFilePath = Path.Combine(Directories.DistDirectory.FullName, htmlFileToPublish.Name);
            File.WriteAllText(destinationFilePath, htmlContent);
        }
    }

    public void Add(DirectoryInfo pageDirectory) // e.g., pageDirectory is src/pages/newPage
    {
        var pageName = pageDirectory.Name; // e.g., "newPage"

        // This HTML is for the *fragment* (e.g., src/pages/newPage/newPage.html)
        // Paths for CSS/JS should be relative to the final HTML file's location in 'bin' root.
        // Page-specific CSS (e.g., newPage.css) is expected at: build/pages/newPage/newPage.css
        // Page-specific JS (e.g., newPage.js) is expected at: build/pages/newPage/newPage.js
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