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
}
