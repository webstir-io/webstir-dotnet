using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Parsing;

namespace Engine.Pipelines.Css.Build;

public static class CssImportProcessor
{
    public static string ProcessForBuild(
        string cssContent,
        string sourceFilePath,
        AppWorkspace workspace,
        DiagnosticCollection? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        DiagnosticCollection diagnosticsLocal = diagnostics ?? new DiagnosticCollection();
        string sourceDirectory = sourceFilePath.DirectoryName();
        string outputPath = ComputeOutputPathForSource(sourceFilePath, workspace);
        string outputDirectory = outputPath.DirectoryName();
        HashSet<string> processedImports = [];

        List<CssImportRule> parsedImports = new CssImportParser(cssContent, sourceFilePath, diagnosticsLocal).ParseImports();
        int parsedIndex = 0;

        RewriteContext context = new(
            sourceDirectory,
            outputDirectory,
            workspace,
            sourceFilePath,
            diagnosticsLocal,
            processedImports
        );

        return CssRegex.Import().Replace(cssContent, match =>
            RewriteImportMatch(match, parsedImports, ref parsedIndex, context));
    }

    internal static string ComputeOutputPathForSource(string sourceFilePath, AppWorkspace workspace)
    {
        string relativePath = Path.GetRelativePath(workspace.FrontendPath, sourceFilePath);
        return workspace.FrontendBuildPath.Combine(relativePath);
    }

    private static (string Path, string? Media) GetImportPathAndMedia(Match match, List<CssImportRule> parsedImports, int index)
    {
        if (index < parsedImports.Count)
        {
            CssImportRule rule = parsedImports[index];
            return (rule.Path, rule.Media);
        }

        string path = match.Groups.Count > 1 ? match.Groups[1].Value : string.Empty;
        string? media = match.Groups.Count > 2 ? match.Groups[2].Value : null;
        return (path, media);
    }

    private static string RewriteImportMatch(
        Match match,
        List<CssImportRule> parsedImports,
        ref int parsedIndex,
        RewriteContext context)
    {
        (string importPath, string? mediaTailContent) = GetImportPathAndMedia(match, parsedImports, parsedIndex);
        parsedIndex++;

        string replacement;
        if (!TryResolveImport(importPath, context.SourceDirectory, context.Workspace.FrontendPath, out string resolvedPath))
        {
            context.Diagnostics.AddError($"Import file not found: {importPath}", context.SourceFilePath);
            replacement = $"/* ERROR: Import file not found: {importPath} */";
        }
        else if (IsCircular(context.ProcessedImports, resolvedPath))
        {
            context.Diagnostics.AddError($"Circular import detected: {importPath}", context.SourceFilePath);
            replacement = $"/* ERROR: Circular import detected: {importPath} */";
        }
        else
        {
            string relativeImportPath = ComputeRelativeImportPath(resolvedPath, context.Workspace.FrontendPath, context.Workspace.FrontendBuildPath, context.OutputDirectory);
            replacement = BuildImport(relativeImportPath, mediaTailContent);
        }

        return replacement;
    }

    private sealed record RewriteContext(
        string SourceDirectory,
        string OutputDirectory,
        AppWorkspace Workspace,
        string SourceFilePath,
        DiagnosticCollection Diagnostics,
        HashSet<string> ProcessedImports
    );

    private static bool TryResolveImport(string importPath, string sourceDirectory, string clientDirectory, out string resolvedPath)
    {
        resolvedPath = CssPathResolver.ResolvePath(importPath, sourceDirectory, clientDirectory);
        return !string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath);
    }

    private static bool IsCircular(HashSet<string> processedImports, string resolvedPath)
        => !processedImports.Add(resolvedPath);

    private static string ComputeRelativeImportPath(string resolvedPath, string clientDirectory, string clientBuildDirectory, string outputDirectory)
    {
        string relativeFromClientRoot = Path.GetRelativePath(clientDirectory, resolvedPath);
        string importedOutputPath = clientBuildDirectory.Combine(relativeFromClientRoot);
        string relativeImportPath = Path.GetRelativePath(outputDirectory, importedOutputPath).Replace('\\', '/');
        return relativeImportPath;
    }

    private static string BuildImport(string relativePath, string? media)
    {
        string mediaTail = string.IsNullOrWhiteSpace(media) ? string.Empty : $" {media.Trim()}";
        return $"@import \"{relativePath}\"{mediaTail};";
    }
}
