using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
using Engine.Processors.Css;

namespace Engine.Workers.Client;

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
            
        // Create app styles directory
        var stylesDirectory = Directories.ClientAppDirectory.SubDirectory("styles");
        
        // Write app.css
        var appCssFilepath = Directories.ClientAppDirectory.Join(_appCssFile);
        if (!File.Exists(appCssFilepath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _appCssFile, appCssFilepath);
        
        // Write styles/reset.css
        var resetCssFilepath = stylesDirectory.Join("reset.css");
        if (!File.Exists(resetCssFilepath))
            AssemblyHelpers.WriteResourceToFile($"{Settings.ClientFolder}.styles", "reset.css", resetCssFilepath);
        
        // Write styles/base.css
        var baseCssFilepath = stylesDirectory.Join("base.css");
        if (!File.Exists(baseCssFilepath))
            AssemblyHelpers.WriteResourceToFile($"{Settings.ClientFolder}.styles", "base.css", baseCssFilepath);

        // Write index.css
        var indexCssOutputFilepath = Directories.ClientIndexDirectory.Join(_indexCssFile);
        if (!File.Exists(indexCssOutputFilepath))
            AssemblyHelpers.WriteResourceToFile(Settings.ClientFolder, _indexCssFile, indexCssOutputFilepath);
    }

    public void Build(bool releaseMode = false)
    {
        // Check if the project uses @import statements
        var appCssFilepath = Directories.ClientAppDirectory.Join(_appCssFile);
        if (File.Exists(appCssFilepath))
        {
            var appCssContent = File.ReadAllText(appCssFilepath);
            if (CssImportProcessor.HasImportStatements(appCssContent))
            {
                // New @import processing
                BuildWithImports(releaseMode);
                return;
            }
        }
        
        // Legacy concatenation mode
        var cssFileLines = MergeAppCssFiles(releaseMode);
        MergePageCssFiles(cssFileLines, releaseMode);

        // The CSS files are already in the correct location (build/pages/)
    }
    
    private static void BuildWithImports(bool releaseMode)
    {
        // Process each page directory
        foreach (var pageDirectory in Directories.ClientPagesDirectory.GetDirectories())
        {
            var pageCssFile = pageDirectory.GetFiles($"{pageDirectory.Name}.css").FirstOrDefault();
            if (pageCssFile == null)
                continue;
                
            // Read the page CSS content
            var cssContent = File.ReadAllText(pageCssFile.FullName);
            
            // Determine output path
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
            
            // Process imports for build mode
            cssContent = CssImportProcessor.ProcessForBuild(cssContent, pageCssFile.FullName, outputFilepath, releaseMode);
            
            // Write the processed CSS
            File.WriteAllText(outputFilepath, cssContent);
        }
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
        // Check if the project uses @import statements
        var appCssFilepath = Directories.ClientAppDirectory.Join(_appCssFile);
        var usesImports = false;
        if (File.Exists(appCssFilepath))
        {
            var appCssContent = File.ReadAllText(appCssFilepath);
            usesImports = CssImportProcessor.HasImportStatements(appCssContent);
        }
        
        // Process root index.css if it exists
        var rootIndexCss = Directories.ClientBuildDirectory.GetFiles("index.css").FirstOrDefault();
        if (rootIndexCss != null)
        {
            var cssContent = File.ReadAllText(rootIndexCss.FullName);
            
            if (usesImports)
            {
                // Process imports for publish mode (inline all imports)
                var sourcePagePath = Path.Combine(Directories.ClientPagesDirectory.FullName, "index", "index.css");
                if (File.Exists(sourcePagePath))
                {
                    cssContent = File.ReadAllText(sourcePagePath);
                    cssContent = CssImportProcessor.ProcessForPublish(cssContent, sourcePagePath);
                }
            }
            
            cssContent = CssMinifier.Minify(cssContent);
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
                
                if (usesImports)
                {
                    // Process imports for publish mode (inline all imports)
                    var sourcePagePath = Path.Combine(Directories.ClientPagesDirectory.FullName, pageDirectory.Name, cssFile.Name);
                    if (File.Exists(sourcePagePath))
                    {
                        cssContent = File.ReadAllText(sourcePagePath);
                        cssContent = CssImportProcessor.ProcessForPublish(cssContent, sourcePagePath);
                    }
                }
                
                cssContent = CssMinifier.Minify(cssContent);
                
                var targetPath = distPageDirectory.Join(cssFile.Name);
                File.WriteAllText(targetPath, cssContent);
            }
        }
    }

    public void AddPage(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        var cssContent = $"/* {pageName} Page Styles */\n@import \"@app/app.css\";\n\n/* Add your page-specific styles here */\n";
        File.WriteAllText(pageDirectory.Join($"{pageName}.css"), cssContent);
    }

}