using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Html.Common;

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

        if (!string.IsNullOrWhiteSpace(manifest.Css))
        {
            string cssHref = FormattableString.Invariant($"/{Folders.Pages}/{pageName}/{manifest.Css}");
            tags.Add(FormattableString.Invariant($"<link rel=\"preload\" as=\"style\" href=\"{cssHref}\" fetchpriority=\"high\">"));
        }

        if (!string.IsNullOrWhiteSpace(manifest.Js))
        {
            string jsHref = FormattableString.Invariant($"/{Folders.Pages}/{pageName}/{manifest.Js}");
            tags.Add(FormattableString.Invariant($"<link rel=\"modulepreload\" href=\"{jsHref}\" fetchpriority=\"high\">"));
        }

        tags.AddRange(BuildPrefetchTags(html, workspace));

        if (tags.Count == 0)
        {
            return html;
        }

        int headClose = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headClose < 0)
        {
            return html;
        }

        string injection = string.Join("\n", tags) + "\n";
        return html.Insert(headClose, injection);
    }

    private static List<string> BuildPrefetchTags(string html, AppWorkspace workspace)
    {
        HashSet<string> added = new(StringComparer.OrdinalIgnoreCase);
        List<string> tags = [];

        foreach (Match m in HtmlRegex.AnchorHref().Matches(html))
        {
            string href = m.Groups["href"].Value;
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || href.StartsWith('#'))
            {
                continue;
            }

            string target = href;
            if (target.Contains('#'))
            {
                target = target[..target.IndexOf('#')];
            }

            if (target.EndsWith(FileExtensions.Html, StringComparison.OrdinalIgnoreCase))
            {
                target = target[..^FileExtensions.Html.Length];
            }

            string pageName = NormalizePageName(target);
            if (string.IsNullOrEmpty(pageName))
            {
                continue;
            }

            if (!added.Add(pageName))
            {
                continue;
            }

            string pageDistDir = Path.Combine(workspace.FrontendDistPath, Folders.Pages, pageName);
            AssetManifest manifest = AssetManifest.Load(pageDistDir);
            if (!string.IsNullOrWhiteSpace(manifest.Js))
            {
                string js = FormattableString.Invariant($"/{Folders.Pages}/{pageName}/{manifest.Js}");
                tags.Add(FormattableString.Invariant($"<link rel=\"prefetch\" href=\"{js}\">"));
            }

            if (tags.Count >= 3)
            {
                break;
            }
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
