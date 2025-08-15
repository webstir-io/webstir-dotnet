using System.Text.RegularExpressions;

namespace Engine.Processors;

public static class CssImportProcessor
{
    private static readonly Regex ImportRegex = new(@"@import\s+(?:url\s*\()?\s*[""']([^""']+)[""']\s*\)?;", RegexOptions.Compiled);
    
    public static bool HasImportStatements(string cssContent)
    {
        return ImportRegex.IsMatch(cssContent);
    }
    
    public static string ProcessForBuild(string cssContent, string sourceFilePath, string outputPath, string clientDirectory, bool releaseMode = false)
    {
        var sourceDir = Path.GetDirectoryName(sourceFilePath) ?? "";
        var outputDir = Path.GetDirectoryName(outputPath) ?? "";
        var processedImports = new HashSet<string>();
        
        return ImportRegex.Replace(cssContent, match =>
        {
            var importPath = match.Groups[1].Value;
            var resolvedPath = CssPathResolver.ResolvePath(importPath, sourceDir, clientDirectory);
            
            if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
                return releaseMode ? "" : $"/* ERROR: Import file not found: {importPath} */";
            
            if (processedImports.Contains(resolvedPath))
                return releaseMode ? "" : $"/* ERROR: Circular import detected: {importPath} */";
            
            processedImports.Add(resolvedPath);
            
            var relativePathFromClient = Path.GetRelativePath(clientDirectory, resolvedPath);
            var buildClientPath = clientDirectory.Replace("src", "build");
            var importedOutputPath = Path.Combine(buildClientPath, relativePathFromClient);            
            var relativeImportPath = Path.GetRelativePath(outputDir, importedOutputPath);
            
            return $"@import \"{relativeImportPath.Replace('\\', '/')}\";";
        });
    }
    
    public static string ProcessForPublish(string cssContent, string sourceFilePath, string clientDirectory, HashSet<string>? processedFiles = null)
    {
        processedFiles ??= new HashSet<string>();
        var sourceDir = Path.GetDirectoryName(sourceFilePath) ?? "";
        
        return ImportRegex.Replace(cssContent, match =>
        {
            var importPath = match.Groups[1].Value;
            var resolvedPath = CssPathResolver.ResolvePath(importPath, sourceDir, clientDirectory);
            
            if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
                return "";
            
            if (processedFiles.Contains(resolvedPath))
                return "";
            
            processedFiles.Add(resolvedPath);
            
            var importedContent = File.ReadAllText(resolvedPath);
            importedContent = ProcessForPublish(importedContent, resolvedPath, clientDirectory, processedFiles);
            
            return importedContent;
        });
    }
}