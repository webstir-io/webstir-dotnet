using CLI.Helpers;
using CLI.Interfaces;
using CLI.Models;
using System.Text.RegularExpressions;

namespace CLI.Workers.Client;

public class StylesWorker : IPageWorker
{
    private const string _appCssFile = "app.css";
    private const string _indexCssFile = "index.css";
    
    // Regex pattern to match @import statements
    private static readonly Regex ImportRegex = new(@"@import\s+(?:url\s*\()?\s*[""']([^""']+)[""']\s*\)?;", RegexOptions.Compiled);
    
    // Namespace mapping for @import resolution
    private static readonly Dictionary<string, string> NamespaceMap = new()
    {
        { "@app/", "app/" },
        { "@components/", "app/components/" },
        { "@shared/", "shared/styles/" },
        { "@pages/", "pages/" }
    };

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
            if (UsesImportStatements(appCssContent))
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
    
    private void BuildWithImports(bool releaseMode)
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
            cssContent = ProcessImportsForBuild(cssContent, pageCssFile.FullName, outputFilepath, releaseMode);
            
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
            usesImports = UsesImportStatements(appCssContent);
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
                    cssContent = ProcessImportsForPublish(cssContent, sourcePagePath);
                }
            }
            
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
                
                if (usesImports)
                {
                    // Process imports for publish mode (inline all imports)
                    var sourcePagePath = Path.Combine(Directories.ClientPagesDirectory.FullName, pageDirectory.Name, cssFile.Name);
                    if (File.Exists(sourcePagePath))
                    {
                        cssContent = File.ReadAllText(sourcePagePath);
                        cssContent = ProcessImportsForPublish(cssContent, sourcePagePath);
                    }
                }
                
                cssContent = RemoveCssComments(cssContent);
                
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
    
    /// <summary>
    /// Checks if CSS content contains @import statements
    /// </summary>
    private static bool UsesImportStatements(string cssContent)
    {
        return ImportRegex.IsMatch(cssContent);
    }
    
    /// <summary>
    /// Processes @import statements for build mode - keeps imports but copies files and rewrites paths
    /// </summary>
    private static string ProcessImportsForBuild(string cssContent, string sourceFilePath, string outputPath, bool releaseMode = false)
    {
        var sourceDir = Path.GetDirectoryName(sourceFilePath) ?? "";
        var outputDir = Path.GetDirectoryName(outputPath) ?? "";
        var processedImports = new HashSet<string>(); // To prevent circular imports
        
        return ImportRegex.Replace(cssContent, match =>
        {
            var importPath = match.Groups[1].Value;
            var resolvedPath = ResolveImportPath(importPath, sourceDir);
            
            if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
            {
                // Return error comment if file not found
                return releaseMode ? "" : $"/* ERROR: Import file not found: {importPath} */";
            }
            
            // Check for circular imports
            if (processedImports.Contains(resolvedPath))
            {
                return releaseMode ? "" : $"/* ERROR: Circular import detected: {importPath} */";
            }
            
            processedImports.Add(resolvedPath);
            
            // Calculate relative path from output location to imported file
            var importedFileName = Path.GetFileName(resolvedPath);
            var importedOutputPath = Path.Combine(outputDir, importedFileName);
            
            // Copy the imported file to the output directory if it's not already there
            if (!File.Exists(importedOutputPath))
            {
                var importedContent = File.ReadAllText(resolvedPath);
                // Recursively process imports in the imported file
                importedContent = ProcessImportsForBuild(importedContent, resolvedPath, importedOutputPath, releaseMode);
                File.WriteAllText(importedOutputPath, importedContent);
            }
            
            // Return the import statement with the new relative path
            return $"@import \"./{importedFileName}\";";
        });
    }
    
    /// <summary>
    /// Processes @import statements for publish mode - inlines all imported content
    /// </summary>
    private static string ProcessImportsForPublish(string cssContent, string sourceFilePath, HashSet<string>? processedFiles = null)
    {
        processedFiles ??= [];
        var sourceDir = Path.GetDirectoryName(sourceFilePath) ?? "";
        
        return ImportRegex.Replace(cssContent, match =>
        {
            var importPath = match.Groups[1].Value;
            var resolvedPath = ResolveImportPath(importPath, sourceDir);
            
            if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
            {
                // Return empty string for missing files in publish mode
                return "";
            }
            
            // Check for circular imports
            if (processedFiles.Contains(resolvedPath))
            {
                return "";
            }
            
            processedFiles.Add(resolvedPath);
            
            // Read and recursively process the imported file
            var importedContent = File.ReadAllText(resolvedPath);
            importedContent = ProcessImportsForPublish(importedContent, resolvedPath, processedFiles);
            
            // Return the inlined content
            return importedContent;
        });
    }
    
    /// <summary>
    /// Resolves an import path considering namespace mappings and relative paths
    /// </summary>
    private static string ResolveImportPath(string importPath, string sourceDir)
    {
        // Handle namespace imports
        foreach (var ns in NamespaceMap)
        {
            if (importPath.StartsWith(ns.Key))
            {
                var relativePath = importPath[ns.Key.Length..];
                var clientPath = Path.Combine(Directories.ClientDirectory.FullName, ns.Value, relativePath);
                return Path.GetFullPath(clientPath);
            }
        }
        
        // Handle relative imports
        if (importPath.StartsWith("./") || importPath.StartsWith("../"))
        {
            var fullPath = Path.Combine(sourceDir, importPath);
            return Path.GetFullPath(fullPath);
        }
        
        // Handle absolute imports from client root
        var absolutePath = Path.Combine(Directories.ClientDirectory.FullName, importPath);
        return Path.GetFullPath(absolutePath);
    }
}