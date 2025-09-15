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

        // Remove all link tags that reference this CSS file (both stylesheet and preload)
        string cssHref = $"/{Folders.Pages}/{pageName}/{cssFileName}";
        string searchPattern = $"href=\"{cssHref}\"";

        int searchIndex = 0;
        while ((searchIndex = html.IndexOf(searchPattern, searchIndex, StringComparison.Ordinal)) >= 0)
        {
            // Find the start of the <link tag
            int linkStart = html.LastIndexOf("<link", searchIndex, StringComparison.OrdinalIgnoreCase);
            if (linkStart >= 0)
            {
                // Find the end of the tag
                int linkEnd = html.IndexOf(">", searchIndex, StringComparison.Ordinal);
                if (linkEnd > linkStart)
                {
                    // Remove the entire link tag (including potential newline after it)
                    int removeEnd = linkEnd + 1;
                    if (removeEnd < html.Length && html[removeEnd] == '\n')
                    {
                        removeEnd++;
                    }

                    html = html.Remove(linkStart, removeEnd - linkStart);

                    // Adjust headCloseIndex if the removal was before it
                    if (linkStart < headCloseIndex)
                    {
                        headCloseIndex -= (removeEnd - linkStart);
                    }

                    // Adjust search index since we removed content
                    searchIndex = linkStart;
                }
                else
                {
                    searchIndex++;
                }
            }
            else
            {
                searchIndex++;
            }
        }

        StringBuilder builder = new(html.Length + cssContent.Length + 128);
        builder.Append(html.AsSpan(0, headCloseIndex));
        builder.Append("\n<style data-critical>\n");
        builder.Append(cssContent);
        builder.Append("\n</style>\n");
        builder.Append(html.AsSpan(headCloseIndex));
        return builder.ToString();
    }
}

