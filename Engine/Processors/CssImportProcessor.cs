using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Engine.Processors.Css;

/// <summary>
/// Handles processing of @import statements in CSS files
/// </summary>
public static class CssImportProcessor
{
    // Regex pattern to match @import statements
    private static readonly Regex ImportRegex = new(@"@import\s+(?:url\s*\()?\s*[""']([^""']+)[""']\s*\)?;", RegexOptions.Compiled);
    
    /// <summary>
    /// Checks if CSS content contains @import statements
    /// </summary>
    public static bool HasImportStatements(string cssContent)
    {
        return ImportRegex.IsMatch(cssContent);
    }
    
    /// <summary>
    /// Processes @import statements for build mode - keeps imports but copies files and rewrites paths
    /// </summary>
    public static string ProcessForBuild(string cssContent, string sourceFilePath, string outputPath, string clientDirectory, bool releaseMode = false)
    {
        var sourceDir = Path.GetDirectoryName(sourceFilePath) ?? "";
        var outputDir = Path.GetDirectoryName(outputPath) ?? "";
        var processedImports = new HashSet<string>(); // To prevent circular imports
        
        return ImportRegex.Replace(cssContent, match =>
        {
            var importPath = match.Groups[1].Value;
            var resolvedPath = CssPathResolver.ResolvePath(importPath, sourceDir, clientDirectory);
            
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
                importedContent = ProcessForBuild(importedContent, resolvedPath, importedOutputPath, clientDirectory, releaseMode);
                File.WriteAllText(importedOutputPath, importedContent);
            }
            
            // Return the import statement with the new relative path
            return $"@import \"./{importedFileName}\";";
        });
    }
    
    /// <summary>
    /// Processes @import statements for publish mode - inlines all imported content
    /// </summary>
    public static string ProcessForPublish(string cssContent, string sourceFilePath, string clientDirectory, HashSet<string>? processedFiles = null)
    {
        processedFiles ??= new HashSet<string>();
        var sourceDir = Path.GetDirectoryName(sourceFilePath) ?? "";
        
        return ImportRegex.Replace(cssContent, match =>
        {
            var importPath = match.Groups[1].Value;
            var resolvedPath = CssPathResolver.ResolvePath(importPath, sourceDir, clientDirectory);
            
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
            importedContent = ProcessForPublish(importedContent, resolvedPath, clientDirectory, processedFiles);
            
            // Return the inlined content
            return importedContent;
        });
    }
}