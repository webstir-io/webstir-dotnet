using CLI.Helpers;
using CLI.Interfaces;
using CLI.Models;

namespace CLI.Workers;

public class StylesWorker : IPageWorker
{
    private const string _appCssFile = "app.css";
    private const string _indexCssFile = "index.css";

    public int BuildOrder { get; } = 3;

    public void Init(ProjectMode mode = ProjectMode.Fullstack)
    {
        // Skip style files for ServerOnly mode
        if (mode == ProjectMode.ServerOnly)
            return;
            
        var appCssFilepath = Directories.ClientAppDirectory.Join(_appCssFile);
        if (!File.Exists(appCssFilepath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _appCssFile, appCssFilepath);

        var indexCssOutputFilepath = Directories.ClientIndexDirectory.Join(_indexCssFile);
        if (!File.Exists(indexCssOutputFilepath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _indexCssFile, indexCssOutputFilepath);
    }

    public void Build(bool releaseMode = false)
    {
        var cssFileLines = MergeAppCssFiles(releaseMode);
        MergePageCssFiles(cssFileLines, releaseMode);

        // The CSS files are already in the correct location (build/pages/)
    }

    private static List<string> MergeAppCssFiles(bool releaseMode)
    {        
        var appCssFilepath = Directories.ClientAppDirectory.Join(_appCssFile);
        var appCssFileLines = File.ReadAllLines(appCssFilepath).ToList();

        foreach (var cssFile in Directories.ClientAppDirectory.GetFiles("*.css", SearchOption.AllDirectories))
        {
            if (cssFile.Name.Equals(_appCssFile))
                continue;

            if (!releaseMode)
            {
                var fileComment = $"{Environment.NewLine}/* {cssFile.Name} */";
                appCssFileLines.Add(fileComment);
            }
            appCssFileLines.AddRange(File.ReadAllLines(cssFile.FullName));
        }

        return appCssFileLines;
    }

    private static void MergePageCssFiles(List<string> appCssFileLines, bool releaseMode)
    {        
        foreach (var pageDirectory in Directories.ClientPagesDirectory.GetDirectories())
        {
            var endOfAppCssPosition = appCssFileLines.Count;
            var mergedCssFileLines = new List<string>(appCssFileLines);

            // Order the files so that numbered screen size css files are applied in the correct order
            var cssFiles = pageDirectory.GetFiles("*.css", SearchOption.AllDirectories).ToList();
            SortFilesWithNumbers(cssFiles);

            foreach (var cssFile in cssFiles)
            {
                var fileLines = File.ReadAllLines(cssFile.FullName);

                // Insert the page css file at the top but after the app.css file.
                if (Path.GetFileNameWithoutExtension(cssFile.Name).Equals(pageDirectory.Name))
                {
                    if (!releaseMode)
                    {
                        var fileComment = $"{Environment.NewLine}/* {cssFile.Name} */";
                        mergedCssFileLines.Insert(endOfAppCssPosition++, fileComment);
                    }
                    mergedCssFileLines.InsertRange(endOfAppCssPosition, fileLines);
                }
                else
                {
                    if (!releaseMode)
                    {
                        var fileComment = $"{Environment.NewLine}/* {cssFile.Name} */";
                        mergedCssFileLines.Add(fileComment);
                    }
                    mergedCssFileLines.AddRange(fileLines);
                }
            }

            // Output CSS directly to page directory (or root for index)
            string outputFilepath;
            if (pageDirectory.Name.Equals("index", StringComparison.OrdinalIgnoreCase))
            {
                outputFilepath = Directories.ClientBuildDirectory.Join($"{pageDirectory.Name}.css");
            }
            else
            {
                outputFilepath = Directories.ClientBuildDirectory
                    .SubDirectory(pageDirectory.Name)
                    .Join($"{pageDirectory.Name}.css");
            }

            File.WriteAllLines(outputFilepath, mergedCssFileLines);
        }
    }

    private static void SortFilesWithNumbers(List<FileInfo> cssFiles)
    {
        cssFiles.Sort((file1, file2) =>
        {
            var file1Number = StringHelpers.ExtractNumber(file1.Name);
            var file2Number = StringHelpers.ExtractNumber(file2.Name);
            return file1Number.CompareTo(file2Number);
        });
    }

    public void Publish()
    {
        // Process root index.css if it exists
        var rootIndexCss = Directories.ClientBuildDirectory.GetFiles("index.css").FirstOrDefault();
        if (rootIndexCss != null)
        {
            var cssContent = File.ReadAllText(rootIndexCss.FullName);
            cssContent = RemoveCssComments(cssContent);
            var targetPath = Directories.ClientDistDirectory.Join(rootIndexCss.Name);
            File.WriteAllText(targetPath, cssContent);
        }
        
        // Process CSS files in page directories
        foreach (var pageDirectory in Directories.ClientBuildDirectory.GetDirectories())
        {
            // Skip non-page directories
            if (pageDirectory.Name == "app" || pageDirectory.Name == "images")
                continue;
                
            var distPageDirectory = Directories.ClientDistDirectory.SubDirectory(pageDirectory.Name);

            foreach (var cssFile in pageDirectory.GetFiles("*.css"))
            {
                var cssContent = File.ReadAllText(cssFile.FullName);
                cssContent = RemoveCssComments(cssContent);
                
                var targetPath = distPageDirectory.Join(cssFile.Name);
                File.WriteAllText(targetPath, cssContent);
            }
        }
    }

    public void AddPage(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        var cssContent = $"/* Styles for {pageName} page */\n";
        File.WriteAllText(pageDirectory.Join($"{pageName}.css"), cssContent);
    }

    private static string RemoveCssComments(string css)
    {
        // Remove CSS comments (/* ... */)
        var commentPattern = @"/\*[\s\S]*?\*/";
        css = System.Text.RegularExpressions.Regex.Replace(css, commentPattern, string.Empty);
        
        // Remove empty lines left by comment removal
        var emptyLinePattern = @"^\s*\r?\n";
        css = System.Text.RegularExpressions.Regex.Replace(
            css, 
            emptyLinePattern, 
            string.Empty, 
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        
        // Trim whitespace from beginning and end
        return css.Trim();
    }
}