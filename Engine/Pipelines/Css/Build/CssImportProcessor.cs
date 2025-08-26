namespace Engine.Pipelines.Css.Build;

public static class CssImportProcessor
{
    public static string ProcessForBuild(string cssContent, string sourceFilePath, string outputPath, string clientDirectory)
    {
        string sourceDir = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
        string outputDir = Path.GetDirectoryName(outputPath) ?? string.Empty;
        HashSet<string> processedImports = [];
        
        return CssRegex.Import().Replace(cssContent, match =>
        {
            string importPath = match.Groups[1].Value;
            string resolvedPath = CssPathResolver.ResolvePath(importPath, sourceDir, clientDirectory);
            
            if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
                return $"/* ERROR: Import file not found: {importPath} */";
            
            if (processedImports.Contains(resolvedPath))
                return $"/* ERROR: Circular import detected: {importPath} */";
            
            processedImports.Add(resolvedPath);
            
            string relativePathFromClient = Path.GetRelativePath(clientDirectory, resolvedPath);
            string buildClientPath = clientDirectory.Replace("src", "build");
            string importedOutputPath = Path.Combine(buildClientPath, relativePathFromClient);
            string relativeImportPath = Path.GetRelativePath(outputDir, importedOutputPath);
            
            return $"@import \"{relativeImportPath.Replace('\\', '/')}\";";
        });
    }
}