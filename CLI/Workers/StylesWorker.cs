using CLI.Helpers;

namespace CLI.Workers;

public class StylesWorker : IWebFileWorker
{
    private const string _appCssFile = "app.css";
    private const string _indexCssFile = "index.css";
    private static readonly string _appCssFilepath = Directories.AppDirectory.Join(_appCssFile);

    public int BuildOrder { get; } = 3;

    public void Init()
    {
        if (!File.Exists(_appCssFilepath))
            AssemblyHelpers.WriteResourceToFile(_appCssFile, _appCssFilepath);

        var _indexCssOutputFilepath = Directories.IndexDirectory.Join(_indexCssFile);
        if (!File.Exists(_indexCssOutputFilepath))
            AssemblyHelpers.WriteResourceToFile(_indexCssFile, _indexCssOutputFilepath);
    }

    public void Build(bool releaseMode = false)
    {
        var cssFileLines = MergeAppCssFiles();
        MergePageCssFiles(cssFileLines);

        // Copy the consolidated css page files to bin folder
        foreach (var mergedPageFile in Directories.BuildPagesDirectory.GetFiles("*.css", SearchOption.AllDirectories))
        {
            var outputFilepath = Directories.BinPagesDirectory
                .SubDirectory(Path.GetFileNameWithoutExtension(mergedPageFile.Name))
                .Join(mergedPageFile.Name);

            File.Copy(mergedPageFile.FullName, outputFilepath);
        }
    }

    private static List<string> MergeAppCssFiles()
    {        
        var appCssFileLines = File.ReadAllLines(_appCssFilepath).ToList();
        
        // if (File.Exists(Directories.AppDirectory.Join("header.css")))
        // {
        //     var headerCssFileLines = File.ReadAllLines(Directories.AppDirectory.Join("header.css"));
        //     appCssFileLines.AddRange(headerCssFileLines);
        // }

        // if (File.Exists(Directories.AppDirectory.Join("footer.css")))
        // {
        //     var footerCssFileLines = File.ReadAllLines(Directories.AppDirectory.Join("footer.css"));
        //     appCssFileLines.AddRange(footerCssFileLines);
        // }

        foreach (var cssFile in Directories.AppDirectory.GetFiles("*.css", SearchOption.AllDirectories))
        {
            if (cssFile.Name.Equals(_appCssFile))
                continue;

            var fileComment = $"{Environment.NewLine}/* {cssFile.Name} */";
            appCssFileLines.Add(fileComment);
            appCssFileLines.AddRange(File.ReadAllLines(cssFile.FullName));
        }

        return appCssFileLines;
    }

    private static void MergePageCssFiles(List<string> appCssFileLines)
    {        
        foreach (var pageDirectory in Directories.PagesDirectory.GetDirectories())
        {
            var endOfAppCssPosition = appCssFileLines.Count;
            var mergedCssFileLines = new List<string>(appCssFileLines);

            // Order the files so that numbered screen size css files are applied in the correct order
            var cssFiles = pageDirectory.GetFiles("*.css", SearchOption.AllDirectories).ToList();
            SortFilesWithNumbers(cssFiles);

            foreach (var cssFile in cssFiles)
            {
                var fileComment = $"{Environment.NewLine}/* {cssFile.Name} */";
                var fileLines = File.ReadAllLines(cssFile.FullName);

                // Insert the page css file at the top but after the app.css file.
                if (Path.GetFileNameWithoutExtension(cssFile.Name).Equals(pageDirectory.Name))
                {
                    mergedCssFileLines.Insert(endOfAppCssPosition++, fileComment);
                    mergedCssFileLines.InsertRange(endOfAppCssPosition, fileLines);
                }
                else
                {
                    mergedCssFileLines.Add(fileComment);
                    mergedCssFileLines.AddRange(fileLines);
                }
            }

            var outputFilepath = Directories.BuildPagesDirectory
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
        foreach (var file in Directories.BinDirectory.GetFiles("*.css"))
            file.CopyTo($"{Directories.DistDirectory.FullName}/{file.Name}");
    }

    public void Add(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        File.Create(pageDirectory.Join($"{pageName}.ts")).Close();
    }
}