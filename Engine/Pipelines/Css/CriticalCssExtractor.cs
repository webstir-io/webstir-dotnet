using System;
using System.IO;
using System.Text;

namespace Engine.Pipelines.Css;

public static class CriticalCssExtractor
{
    private const int InlineThresholdBytes = 6 * 1024; // 6KB budget

    public static string InlineCriticalCss(string html, string pageName, string pageDistDirectory)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(pageName);
        ArgumentNullException.ThrowIfNull(pageDistDirectory);

        int headCloseIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headCloseIndex < 0)
        {
            return html;
        }

        string cssLinkPrefix = FormattableString.Invariant($"href=\"/{Folders.Pages}/{pageName}/");
        int linkIndex = html.IndexOf(cssLinkPrefix, StringComparison.Ordinal);
        if (linkIndex < 0)
        {
            return html;
        }

        int hrefStart = linkIndex + cssLinkPrefix.Length;
        int hrefEnd = html.IndexOf('"', hrefStart);
        if (hrefEnd <= hrefStart)
        {
            return html;
        }

        string cssFileName = html[hrefStart..hrefEnd];
        if (!cssFileName.EndsWith(FileExtensions.Css, StringComparison.OrdinalIgnoreCase))
        {
            return html;
        }

        string cssPath = Path.Combine(pageDistDirectory, cssFileName.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(cssPath))
        {
            return html;
        }

        FileInfo info = new(cssPath);
        if (info.Length > InlineThresholdBytes)
        {
            return html;
        }

        string cssContent = File.ReadAllText(cssPath);
        StringBuilder builder = new(html.Length + cssContent.Length + 128);
        builder.Append(html.AsSpan(0, headCloseIndex));
        builder.Append("\n<style data-critical>\n");
        builder.Append(cssContent);
        builder.Append("\n</style>\n");
        builder.Append(html.AsSpan(headCloseIndex));
        return builder.ToString();
    }
}

