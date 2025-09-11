using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using CssConstants = Engine.Pipelines.Css.Common.Css;
using Engine.Pipelines.Css.Common;
using Engine.Pipelines.Css.Models;

namespace Engine.Pipelines.Css.Parsing;

public static class CssParser
{
    public static List<CssImport> ExtractImports(string content, string baseDirectory)
    {
        List<CssImport> imports = [];
        CssImportParser parser = new(content, baseDirectory);
        List<CssImportRule> parsed = parser.ParseImports();
        foreach (CssImportRule item in parsed)
        {
            string importPath = item.Path;
            string? media = item.Media;
            imports.Add(new CssImport
            {
                Path = importPath,
                ResolvedPath = ResolvePath(importPath, baseDirectory),
                Media = media,
                IsModuleImport = importPath.EndsWith(CssConstants.ModuleExt, StringComparison.OrdinalIgnoreCase)
            });
        }
        return imports;
    }

    public static HashSet<string> ExtractClassNames(string content)
    {
        HashSet<string> classNames = [];
        MatchCollection matches = CssRegex.ClassSelector().Matches(content);

        foreach (Match match in matches)
        {
            classNames.Add(match.Groups[1].Value);
        }

        return classNames;
    }

    public static List<string> ExtractUrls(string content)
    {
        List<string> urls = [];
        MatchCollection matches = CssRegex.Url().Matches(content);

        foreach (Match match in matches)
        {
            // Groups: 1=quote, 2=quoted inner, 3=unquoted inner
            string inner = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
            urls.Add(inner);
        }

        return urls;
    }

    public static string UpdateUrls(string content, Func<string, string> urlTransformer)
    {
        return CssRegex.Url().Replace(content, match =>
        {
            // Groups: 1=quote, 2=quoted inner, 3=unquoted inner
            string inner = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
            string originalFull = match.Value;

            // Keep data URIs exactly as-is to avoid breaking content
            if (inner.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return originalFull;
            }

            string transformed = urlTransformer(inner);

            if (RequiresQuoting(transformed))
            {
                return $"url(\"{EscapeCssString(transformed)}\")";
            }

            return $"url({transformed})";
        });
    }

    private static bool RequiresQuoting(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c) || c == '(' || c == ')' || c == '\'' || c == '"' || c == '\\')
            {
                return true;
            }
        }
        return false;
    }

    private static string EscapeCssString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private static string ResolvePath(string importPath, string baseDirectory)
    {
        if (Path.IsPathRooted(importPath))
        {
            return importPath;
        }

        string resolved = Path.GetFullPath(Path.Combine(baseDirectory, importPath));

        if (!Path.HasExtension(resolved))
        {
            resolved += CssConstants.CssExt;
        }

        return resolved;
    }
}
