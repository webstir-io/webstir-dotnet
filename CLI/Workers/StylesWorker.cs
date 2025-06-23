using CLI.Helpers;
using CLI.Interfaces;

namespace CLI.Workers;

public class StylesWorker : IFileWorker
{
    private const string _appCssFile = "app.css";
    private const string _indexCssFile = "index.css";
    private static readonly string _appCssFilepath = Directories.ClientAppDirectory.Join(_appCssFile);

    public int BuildOrder { get; } = 3;

    public void Init()
    {
        if (!File.Exists(_appCssFilepath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _appCssFile, _appCssFilepath);

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
        var appCssFileLines = File.ReadAllLines(_appCssFilepath).ToList();

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

            var outputFilepath = Directories.ClientBuildPagesDirectory
                .SubDirectory(pageDirectory.Name)
                .Join($"{pageDirectory.Name}.css");

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
        // No app.css to copy in the current architecture
        
        if (Directories.ClientBuildPagesDirectory.Exists)
        {
            foreach (var pageDirectory in Directories.ClientBuildPagesDirectory.GetDirectories())
            {
                var distPagesDirectory = Directories.ClientDistPagesDirectory.SubDirectory(pageDirectory.Name);
                distPagesDirectory.Create();

                foreach (var cssFile in pageDirectory.GetFiles("*.css"))
                {
                    var cssContent = File.ReadAllText(cssFile.FullName);
                    cssContent = RemoveCssComments(cssContent);
                    
                    var targetPath = distPagesDirectory.Join(cssFile.Name);
                    File.WriteAllText(targetPath, cssContent);
                }
            }
        }
    }

    public void Add(DirectoryInfo pageDirectory)
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