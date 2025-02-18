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
        var appHtmlFilepath = Directories.AppDirectory.Join(_appHtmlFile);
        if (!File.Exists(appHtmlFilepath))
            throw new Exception($"Could not locate the required file {appHtmlFilepath};");

        // Merge the app.html file with the page html file and write to intermediate build folder
        var appHtmlFile = new HtmlFile(appHtmlFilepath);
        foreach (var pageDirectory in Directories.PagesDirectory.GetDirectories())
        foreach (var pageFile in pageDirectory.GetFiles("*.html"))
        {
            var htmlFile = new HtmlFile(pageFile.FullName);
            var mergedHtml = appHtmlFile.Merge(htmlFile.Html);
            var outputFilepath = Directories.BuildPagesDirectory
                .SubDirectory(pageDirectory.Name)
                .Join(pageFile.Name);
            
            File.WriteAllText(outputFilepath, mergedHtml);
        }

        // Copy merged page html files to bin folder
        foreach (var mergedPageFile in Directories.BuildPagesDirectory.GetFiles("*.html", SearchOption.AllDirectories))
        {
            var outputFilepath = Directories.BinDirectory.Join(mergedPageFile.Name);
            File.Copy(mergedPageFile.FullName, outputFilepath);
        }
    }

    public void Publish()
    {
        foreach (var file in Directories.BinDirectory.GetFiles("*.html"))
            file.CopyTo($"{Directories.DistDirectory.FullName}/{file.Name}");
    }
}