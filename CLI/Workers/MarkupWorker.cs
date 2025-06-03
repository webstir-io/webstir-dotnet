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
            // Assuming app.html references refresh.js from the root, e.g., <script src="refresh.js" async>
            // Ensure this path is correct based on where app.html is and where refresh.js will be in 'bin'.
            // If refresh.js is at bin root, and app.html is processed as a base, this path is fine.
            appHtmlFile.Remove(@$"    <script src=""refresh.js"" async></script>{Environment.NewLine}");
        }

        Directory.CreateDirectory(Directories.BinDirectory.FullName); // Ensure bin directory exists

        foreach (var pageSourceDirectory in Directories.PagesDirectory.GetDirectories()) // e.g., src/pages/index
        {
            foreach (var pageHtmlFragmentFile in pageSourceDirectory.GetFiles("*.html")) // e.g., src/pages/index/index.html
            {
                var pageFragment = new HtmlFile(pageHtmlFragmentFile.FullName);
                string mergedHtmlContent = appHtmlFile.Merge(pageFragment.Html);

                // Output path will be like build/bin/index.html or build/bin/login.html
                string outputFilePath = Path.Combine(Directories.BinDirectory.FullName, pageHtmlFragmentFile.Name);

                File.WriteAllText(outputFilePath, mergedHtmlContent);
            }
        }
    }

    public void Publish()
    {
        Directory.CreateDirectory(Directories.DistDirectory.FullName); // Ensure dist directory exists

        // Copy all HTML files from the root of the bin directory to the dist directory
        foreach (var htmlFileToPublish in Directories.BinDirectory.GetFiles("*.html", SearchOption.TopDirectoryOnly))
        {
            string destinationFilePath = Path.Combine(Directories.DistDirectory.FullName, htmlFileToPublish.Name);
            htmlFileToPublish.CopyTo(destinationFilePath, true);
        }
    }

    public void Add(DirectoryInfo pageDirectory) // e.g., pageDirectory is src/pages/newPage
    {
        var pageName = pageDirectory.Name; // e.g., "newPage"

        // This HTML is for the *fragment* (e.g., src/pages/newPage/newPage.html)
        // Paths for CSS/JS should be relative to the final HTML file's location in 'bin' root.
        // Page-specific CSS (e.g., newPage.css) is expected at: build/bin/newPage.css
        // Page-specific JS (e.g., newPage.js) is expected at: build/bin/pages/newPage/newPage.js
        var baseHtmlFragment =
$@"<head>
    <title>{pageName}</title>
    <link rel=""stylesheet"" href=""pages/{pageName}{pageName}.css"" />
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
}