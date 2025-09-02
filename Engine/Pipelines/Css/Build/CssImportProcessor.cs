using Engine.Extensions;
using Engine.Pipelines.Core.Parsing;

namespace Engine.Pipelines.Css.Build;

public static class CssImportProcessor
{
    public static string ProcessForBuild(string cssContent, string sourceFilePath, string outputPath, string clientDirectory, string clientBuildDirectory)
    {
        string sourceDir = sourceFilePath.DirectoryName();
        string outputDir = outputPath.DirectoryName();
        HashSet<string> processedImports = [];

        // Parse imports via tokenizer to handle whitespace/comments/media robustly
        CssImportParser parser = new(cssContent, sourceFilePath);
        List<CssImportRule> parsed = parser.ParseImports();
        int index = 0;

        return CssRegex.Import().Replace(cssContent, match =>
        {
            // Defensive: if regex finds more than tokenizer, fall back to the matched path
            string importPath = index < parsed.Count ? parsed[index].Path : match.Groups[1].Value;
            string? media = index < parsed.Count ? parsed[index].Media : match.Groups.Count > 2 ? match.Groups[2].Value : null;
            index++;

            string resolvedPath = CssPathResolver.ResolvePath(importPath, sourceDir, clientDirectory);
            if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
            {
                return $"/* ERROR: Import file not found: {importPath} */";
            }

            if (!processedImports.Add(resolvedPath))
            {
                return $"/* ERROR: Circular import detected: {importPath} */";
            }

            string relativePathFromClient = Path.GetRelativePath(clientDirectory, resolvedPath);
            string importedOutputPath = clientBuildDirectory.Combine(relativePathFromClient);
            string relativeImportPath = Path.GetRelativePath(outputDir, importedOutputPath).Replace('\\', '/');

            // Preserve media tail when present
            string mediaTail = string.IsNullOrWhiteSpace(media) ? string.Empty : $" {media.Trim()}";
            return $"@import \"{relativeImportPath}\"{mediaTail};";
        });
    }
}
