using System;
using System.IO;
using System.Text.RegularExpressions;
using Engine.Pipelines.Css;

namespace Engine.Pipelines.Html;

public static class FontPreloadInjector
{
    public static string InjectFromCss(string html, string cssFilePath, string pageName)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(cssFilePath);
        ArgumentNullException.ThrowIfNull(pageName);

        if (!File.Exists(cssFilePath))
            return html;

        string? fontUrl = ExtractFirstFontUrl(cssFilePath);
        if (fontUrl == null)
            return html;

        return InjectPreloadTag(html, fontUrl);
    }

    private static string? ExtractFirstFontUrl(string cssFilePath)
    {
        string css = File.ReadAllText(cssFilePath);

        foreach (Match block in CssRegex.FontFaceWithSrc().Matches(css))
        {
            Match src = CssRegex.FontSrcDecl().Match(block.Value);
            if (!src.Success)
                continue;

            string? fontUrl = ExtractWoffUrl(src.Groups[1].Value);
            if (fontUrl != null)
                return fontUrl;
        }

        return null;
    }

    private static string? ExtractWoffUrl(string srcValue)
    {
        foreach (Match m in CssRegex.FontUrlExtractor().Matches(srcValue))
        {
            string url = m.Groups["url"].Value;

            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;

            string lower = url.ToLowerInvariant();
            if (lower.EndsWith(FileExtensions.Woff2, StringComparison.Ordinal) ||
                lower.EndsWith(FileExtensions.Woff, StringComparison.Ordinal))
            {
                return url;
            }
        }

        return null;
    }

    private static string InjectPreloadTag(string html, string fontUrl)
    {
        string href = NormalizeUrl(fontUrl);
        string type = GetFontMimeType(href);
        string preload = FormattableString.Invariant($"<link rel=\"preload\" as=\"font\" type=\"{type}\" href=\"{href}\" crossorigin>");

        int headClose = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headClose < 0)
            return html;

        return html.Insert(headClose, preload + "\n");
    }

    private static string NormalizeUrl(string url)
    {
        if (url.StartsWith('/'))
            return url;

        return "/" + url.TrimStart('.').TrimStart('/');
    }

    private static string GetFontMimeType(string url)
    {
        return url.EndsWith(FileExtensions.Woff2, StringComparison.OrdinalIgnoreCase)
            ? "font/woff2"
            : "font/woff";
    }
}
