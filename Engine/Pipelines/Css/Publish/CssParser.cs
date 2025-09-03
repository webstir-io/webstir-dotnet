using System.Text.RegularExpressions;

using Engine.Pipelines.Core.Parsing;
using Engine.Pipelines.Css.Models;

namespace Engine.Pipelines.Css.Publish;

public static class Parser
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
                IsModuleImport = importPath.EndsWith(Css.ModuleExt, StringComparison.OrdinalIgnoreCase)
            });
        }
        return imports;
    }

    public static HashSet<string> ExtractClassNames(string content)
    {
        HashSet<string> classNames = [];
        MatchCollection matches = CssRegex.ClassSelector().Matches(content);

        foreach (Match match in matches)
            classNames.Add(match.Groups[1].Value);

        return classNames;
    }

    public static List<string> ExtractUrls(string content)
    {
        List<string> urls = [];
        MatchCollection matches = CssRegex.Url().Matches(content);

        foreach (Match match in matches)
            urls.Add(match.Groups[1].Value);

        return urls;
    }

    public static string UpdateUrls(string content, Func<string, string> urlTransformer)
    {
        return CssRegex.Url().Replace(content, match =>
        {
            string url = match.Groups[1].Value;
            string transformed = urlTransformer(url);
            return $"url({transformed})";
        });
    }

    private static string ResolvePath(string importPath, string baseDirectory)
    {
        if (Path.IsPathRooted(importPath))
            return importPath;

        string resolved = Path.GetFullPath(Path.Combine(baseDirectory, importPath));

        if (!Path.HasExtension(resolved))
            resolved += Css.CssExt;

        return resolved;
    }
}
