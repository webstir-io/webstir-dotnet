using System;
using System.Text.RegularExpressions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Html.Common;
using Engine.Pipelines.Html.Parsing;

namespace Engine.Pipelines.Html.Transformation;

public static class HtmlTransformer
{
    public static string MergeTemplates(string appTemplate, string pageFragment)
    {
        string result = appTemplate;

        (string? headContent, string? mainContent) = HtmlParser.ExtractSections(pageFragment);

        if (headContent != null)
        {
            result = HtmlRegex.CloseHeadTag().Replace(result, $"{headContent}\n</head>");
        }

        if (mainContent != null)
        {
            result = HtmlRegex.EmptyMain().Replace(result, $"<main$1>{mainContent}</main>");
        }

        return result;
    }

    public static string RewriteAssetReferences(string html, AssetManifest manifest, string pageName)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        string result = html;

        if (!string.IsNullOrWhiteSpace(manifest.Css))
        {
            string cssPath = $"/{Folders.Pages}/{pageName}/{manifest.Css}";
            result = RewriteAssetPath(result, $"{Files.Index}{FileExtensions.Css}", cssPath);
            result = RewriteAssetPath(result, $"{Files.Index}.module{FileExtensions.Css}", cssPath);
        }

        if (!string.IsNullOrWhiteSpace(manifest.Js))
        {
            string jsPath = $"/{Folders.Pages}/{pageName}/{manifest.Js}";
            result = RewriteAssetPath(result, $"{Files.Index}{FileExtensions.Js}", jsPath);
        }

        return result;
    }

    private static string RewriteAssetPath(string html, string oldPath, string newPath)
    {
        html = html.Replace($"\"{oldPath}\"", $"\"{newPath}\"");
        html = html.Replace($"'{oldPath}'", $"'{newPath}'");
        return html;
    }

    public static string AddImageDimensions(string html, string pageBuildDirectory)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(pageBuildDirectory);

        // Base directory examples: build/frontend/pages/<page>/
        // Root (build/frontend) for absolute paths
        string? pagesDir = System.IO.Path.GetDirectoryName(pageBuildDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        string? rootDir = pagesDir != null ? System.IO.Path.GetDirectoryName(pagesDir) : null;

        return HtmlRegex.ImgTag().Replace(html, match =>
        {
            string tag = match.Value;
            // Skip if width/height already exist
            if (HtmlRegex.WidthAttr().IsMatch(tag) || HtmlRegex.HeightAttr().IsMatch(tag))
            {
                return tag;
            }

            Match srcMatch = HtmlRegex.ImgSrc().Match(tag);
            if (!srcMatch.Success)
            {
                return tag;
            }

            string src = srcMatch.Groups["src"].Value;
            if (src.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || src.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return tag;
            }

            string? fullPath = null;
            try
            {
                if (src.StartsWith("/", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(rootDir))
                    {
                        fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(rootDir, src.TrimStart('/').Replace('/', System.IO.Path.DirectorySeparatorChar)));
                    }
                }
                else
                {
                    fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(pageBuildDirectory, src.Replace('/', System.IO.Path.DirectorySeparatorChar)));
                }
            }
            catch
            {
                fullPath = null;
            }

            if (string.IsNullOrEmpty(fullPath) || !System.IO.File.Exists(fullPath))
            {
                return tag;
            }

            if (!Engine.Pipelines.Images.ImageOptimizer.TryGetImageDimensions(fullPath, out int width, out int height))
            {
                return tag;
            }

            // Inject width and height before closing '>'
            int insertIndex = tag.LastIndexOf('>');
            if (insertIndex <= 0)
            {
                return tag;
            }
            string injection = FormattableString.Invariant($" width=\"{width}\" height=\"{height}\"");
            string updated = tag.Insert(insertIndex, injection);
            return updated;
        });
    }
}
