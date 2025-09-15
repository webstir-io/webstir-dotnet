using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Engine.Pipelines.Core;

namespace Engine.Pipelines.Html;

public static class ResourceHintInjector
{
    public static string Inject(string html, AssetManifest manifest, string pageName, AppWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(pageName);
        ArgumentNullException.ThrowIfNull(workspace);

        List<string> tags = [];
        tags.AddRange(BuildPrefetchTags(html, workspace));
        if (tags.Count == 0)
        {
            return html;
        }

        // Find and inject after the opening <head> tag
        int headOpen = html.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
        if (headOpen < 0)
        {
            return html;
        }

        int openEnd = html.IndexOf('>', headOpen);
        if (openEnd <= headOpen)
        {
            return html;
        }

        string injection = "\n" + string.Join("\n", tags) + "\n";
        return html.Insert(openEnd + 1, injection);
    }

    private static List<string> BuildPrefetchTags(string html, AppWorkspace workspace)
    {
        List<string> tags = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in HtmlRegex.AnchorHref().Matches(html))
        {
            string href = m.Groups["href"].Value;
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith('#')
                || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string target = NormalizePageName(href);
            if (string.IsNullOrEmpty(target))
            {
                continue;
            }

            if (!seen.Add(target))
            {
                continue;
            }

            string docHref = FormattableString.Invariant($"/{Folders.Pages}/{target}/{Files.Index}{FileExtensions.Html}");
            tags.Add(FormattableString.Invariant($"<link rel=\"prefetch\" href=\"{docHref}\" as=\"document\">"));
        }

        return tags;
    }

    private static string NormalizePageName(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return string.Empty;
        }

        string path = href;
        if (path.StartsWith('/'))
        {
            path = path.TrimStart('/');
        }

        if (path.StartsWith(Folders.Pages + '/', StringComparison.Ordinal))
        {
            path = path[(Folders.Pages.Length + 1)..];
        }

        if (path.EndsWith('/'))
        {
            path = path[..^1];
        }

        int slashIndex = path.IndexOf('/');
        if (slashIndex >= 0)
        {
            path = path[..slashIndex];
        }

        return path;
    }
}
