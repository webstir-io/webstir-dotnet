using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Html.Common;
using Engine.Pipelines.Html.Parsing;

namespace Engine.Pipelines.Html.Transformation;

public static partial class HtmlTransformer
{
    public static string MergeTemplates(string appTemplate, string pageFragment)
    {
        ArgumentNullException.ThrowIfNull(appTemplate);
        ArgumentNullException.ThrowIfNull(pageFragment);
        string result = appTemplate;

        (string? pageHeadContent, string? pageMainContent) = HtmlParser.ExtractSections(pageFragment);

        // Merge <head>: preserve template head, replace unique meta/link with page values, then append remaining page head
        Match appHeadMatch = HtmlRegex.HeadContent().Match(appTemplate);
        if (appHeadMatch.Success && pageHeadContent is not null)
        {
            string appHeadInner = appHeadMatch.Groups[1].Value;
            string mergedHeadInner = MergeHeadInner(appHeadInner, pageHeadContent);

            // Replace the inner content of <head> with merged content
            int innerIndex = appHeadMatch.Groups[1].Index;
            int innerLength = appHeadMatch.Groups[1].Length;
            StringBuilder sb = new(appTemplate.Length - innerLength + mergedHeadInner.Length);
            sb.Append(appTemplate.AsSpan(0, innerIndex));
            sb.Append(mergedHeadInner);
            sb.Append(appTemplate.AsSpan(innerIndex + innerLength));
            result = sb.ToString();
        }
        else if (pageHeadContent is not null)
        {
            // Fallback: if head content extraction failed on app template for some reason, append page head
            result = HtmlRegex.CloseHeadTag().Replace(result, $"{pageHeadContent}\n</head>");
        }

        // Merge <main>
        if (pageMainContent is not null)
        {
            result = HtmlRegex.EmptyMain().Replace(result, $"<main$1>{pageMainContent}</main>");
        }

        // Ensure href/src attribute values are quoted to avoid browsers capturing the tag-closing '/>'
        result = QuoteUnquotedHrefSrc(result);
        return result;
    }

    public static string RewriteAssetReferences(string html, AssetManifest manifest, string pageName)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        string result = html;

        if (!string.IsNullOrWhiteSpace(manifest.Css))
        {
            // Use absolute path to ensure correct resolution even when the page is served at '/'
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
        // Quoted attribute values
        html = html.Replace($"\"{oldPath}\"", $"\"{newPath}\"");
        html = html.Replace($"'{oldPath}'", $"'{newPath}'");
        // Quoting for unquoted values handled in a separate pass
        return html;
    }

    private static string QuoteUnquotedHrefSrc(string html)
    {
        // href=VALUE -> href="VALUE" (when VALUE is unquoted)
        html = UnquotedHrefRegex().Replace(html, m => $"href=\"{m.Groups[1].Value}\"");
        // src=VALUE -> src="VALUE"
        html = UnquotedSrcRegex().Replace(html, m => $"src=\"{m.Groups[1].Value}\"");
        return html;
    }

    [GeneratedRegex(@"href=([^""'\s>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex UnquotedHrefRegex();

    [GeneratedRegex(@"src=([^""'\s>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex UnquotedSrcRegex();

    public static string AddImageDimensions(string html, string pageBuildDirectory)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(pageBuildDirectory);

        // Base directory examples: build/frontend/pages/<page>/
        // Root (build/frontend) for absolute paths
        string? pagesDir = Path.GetDirectoryName(pageBuildDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string? rootDir = pagesDir != null ? Path.GetDirectoryName(pagesDir) : null;

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
                        fullPath = Path.GetFullPath(Path.Combine(rootDir, src.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                    }
                }
                else
                {
                    fullPath = Path.GetFullPath(Path.Combine(pageBuildDirectory, src.Replace('/', Path.DirectorySeparatorChar)));
                }
            }
            catch
            {
                fullPath = null;
            }

            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                return tag;
            }

            if (!Images.ImageOptimizer.TryGetImageDimensions(fullPath, out int width, out int height))
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

    private static string MergeHeadInner(string appHeadInner, string pageHeadInner)
    {
        // 1) Find unique keyed nodes in app and page
        List<HeadNode> appNodes = FindHeadNodes(appHeadInner);
        List<HeadNode> pageNodes = FindHeadNodes(pageHeadInner);

        Dictionary<string, HeadNode> pagePreferredByKey = new(StringComparer.Ordinal);
        Dictionary<string, List<HeadNode>> pageAllByKey = new(StringComparer.Ordinal);
        foreach (HeadNode node in pageNodes)
        {
            if (node.Key is null)
            {
                continue;
            }
            pagePreferredByKey[node.Key] = node; // last wins
            if (!pageAllByKey.TryGetValue(node.Key, out List<HeadNode>? list))
            {
                list = [];
                pageAllByKey[node.Key] = list;
            }
            list.Add(node);
        }

        // 2) Replace app nodes for keys present in page (prepare replacements on appHeadInner)
        List<(int Index, int Length, string Replacement)> appReplacements = [];
        HashSet<string> keysReplaced = new(StringComparer.Ordinal);
        foreach (HeadNode appNode in appNodes)
        {
            if (appNode.Key is null)
            {
                continue;
            }
            if (pagePreferredByKey.TryGetValue(appNode.Key, out HeadNode preferred))
            {
                appReplacements.Add((appNode.Index, appNode.Length, preferred.Original));
                keysReplaced.Add(appNode.Key);
            }
        }

        string merged = ApplyChanges(appHeadInner, appReplacements);

        // 3) Deduplicate page content: keep only preferred per key; and drop ones that were used to replace
        List<(int Index, int Length)> pageRemovals = [];
        foreach (KeyValuePair<string, List<HeadNode>> kvp in pageAllByKey)
        {
            List<HeadNode> list = kvp.Value;
            for (int i = 0; i < list.Count - 1; i++)
            {
                pageRemovals.Add((list[i].Index, list[i].Length)); // remove all but last
            }
        }

        // Remove preferred page nodes that were used for replacement (avoid duplication on append)
        foreach (string key in keysReplaced)
        {
            HeadNode preferred = pagePreferredByKey[key];
            pageRemovals.Add((preferred.Index, preferred.Length));
        }

        string filteredPageHead = ApplyRemovals(pageHeadInner, pageRemovals);

        if (!string.IsNullOrWhiteSpace(filteredPageHead))
        {
            if (merged.Length > 0 && !merged.EndsWith("\n", StringComparison.Ordinal))
            {
                merged += "\n";
            }
            merged += filteredPageHead;
        }

        return ReorderHeadStandards(merged);
    }

    private static List<HeadNode> FindHeadNodes(string headInner)
    {
        List<HeadNode> nodes = [];

        foreach (Match m in HtmlRegex.MetaTag().Matches(headInner))
        {
            string? key = BuildMetaKey(m.Value);
            nodes.Add(new HeadNode(m.Index, m.Length, m.Value, key));
        }

        foreach (Match m in HtmlRegex.LinkTag().Matches(headInner))
        {
            string? key = BuildLinkKey(m.Value);
            nodes.Add(new HeadNode(m.Index, m.Length, m.Value, key));
        }

        return nodes;
    }

    private static string? BuildMetaKey(string tag)
    {
        Dictionary<string, string> attrs = ParseAttributes(tag);
        if (attrs.ContainsKey("charset"))
        {
            return "meta:charset";
        }
        if (attrs.TryGetValue("http-equiv", out string? httpEquiv))
        {
            return FormattableString.Invariant($"meta:http-equiv:{httpEquiv.ToLowerInvariant()}");
        }
        if (attrs.TryGetValue("name", out string? name))
        {
            return FormattableString.Invariant($"meta:name:{name.ToLowerInvariant()}");
        }
        if (attrs.TryGetValue("property", out string? prop))
        {
            return FormattableString.Invariant($"meta:property:{prop.ToLowerInvariant()}");
        }
        return null;
    }

    private static string? BuildLinkKey(string tag)
    {
        Dictionary<string, string> attrs = ParseAttributes(tag);
        if (attrs.TryGetValue("rel", out string? rel))
        {
            // split rel by whitespace
            string[] parts = rel.Split((char[])[' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                if (part.Equals("canonical", StringComparison.OrdinalIgnoreCase))
                {
                    return "link:rel:canonical";
                }
                if (part.Equals("alternate", StringComparison.OrdinalIgnoreCase))
                {
                    if (attrs.TryGetValue("hreflang", out string? hreflang) && !string.IsNullOrEmpty(hreflang))
                    {
                        return FormattableString.Invariant($"link:rel:alternate:{hreflang.ToLowerInvariant()}");
                    }
                }
            }
        }
        return null;
    }

    private static Dictionary<string, string> ParseAttributes(string tag)
    {
        // Extract inside of tag: <name ...>
        int lt = tag.IndexOf('<');
        int gt = tag.IndexOf('>');
        if (lt < 0 || gt <= lt)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        int i = lt + 1;
        while (i < gt && !IsAsciiWhitespace(tag[i]))
        {
            i++;
        }

        Dictionary<string, string> attrs = new(StringComparer.OrdinalIgnoreCase);
        while (i < gt)
        {
            // skip whitespace
            while (i < gt && IsAsciiWhitespace(tag[i]))
                i++;
            if (i >= gt)
                break;

            // read name
            int nameStart = i;
            while (i < gt && !IsAsciiWhitespace(tag[i]) && tag[i] != '=' && tag[i] != '/' && tag[i] != '>')
                i++;
            if (nameStart == i)
            {
                i++;
                continue;
            }
            string name = tag[nameStart..i];

            // skip whitespace
            while (i < gt && IsAsciiWhitespace(tag[i]))
                i++;

            string value = string.Empty;
            if (i < gt && tag[i] == '=')
            {
                i++;
                while (i < gt && IsAsciiWhitespace(tag[i]))
                    i++;
                if (i < gt && (tag[i] == '"' || tag[i] == '\''))
                {
                    char quote = tag[i++];
                    int valStart = i;
                    while (i < gt && tag[i] != quote)
                        i++;
                    value = tag[valStart..Math.Min(i, gt)];
                    if (i < gt && tag[i] == quote)
                        i++;
                }
                else
                {
                    int valStart = i;
                    while (i < gt && !IsAsciiWhitespace(tag[i]) && tag[i] != '/' && tag[i] != '>')
                        i++;
                    value = tag[valStart..Math.Min(i, gt)];
                }
            }

            if (!string.IsNullOrEmpty(name))
            {
                attrs[name] = value;
            }
        }

        return attrs;
    }

    private static bool IsAsciiWhitespace(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f' or '\v';

    private static string ApplyChanges(string source, List<(int Index, int Length, string Replacement)> changes)
    {
        if (changes.Count == 0)
        {
            return source;
        }
        changes.Sort((a, b) => b.Index.CompareTo(a.Index));
        StringBuilder sb = new(source);
        foreach ((int Index, int Length, string Replacement) change in changes)
        {
            sb.Remove(change.Index, change.Length);
            sb.Insert(change.Index, change.Replacement);
        }
        return sb.ToString();
    }

    private static string ApplyRemovals(string source, List<(int Index, int Length)> removals)
    {
        if (removals.Count == 0)
        {
            return source;
        }
        removals.Sort((a, b) => b.Index.CompareTo(a.Index));
        StringBuilder sb = new(source);
        foreach ((int Index, int Length) rem in removals)
        {
            sb.Remove(rem.Index, rem.Length);
        }
        return sb.ToString();
    }

    private readonly record struct HeadNode(int Index, int Length, string Original, string? Key);

    private static string ReorderHeadStandards(string headInner)
    {
        if (string.IsNullOrEmpty(headInner))
        {
            return headInner;
        }

        int charsetIndex = -1, charsetLength = 0;
        int viewportIndex = -1, viewportLength = 0;
        string? charsetTag = null;
        string? viewportTag = null;

        foreach (Match m in HtmlRegex.MetaTag().Matches(headInner))
        {
            Dictionary<string, string> attrs = ParseAttributes(m.Value);
            if (charsetIndex < 0 && attrs.ContainsKey("charset"))
            {
                charsetIndex = m.Index;
                charsetLength = m.Length;
                charsetTag = m.Value;
                continue;
            }
            if (attrs.TryGetValue("name", out string? name) && name.Equals("viewport", StringComparison.OrdinalIgnoreCase))
            {
                viewportIndex = m.Index;
                viewportLength = m.Length;
                viewportTag = m.Value;
            }
        }

        List<(int Index, int Length)> removals = [];
        if (viewportIndex >= 0)
        {
            removals.Add((viewportIndex, viewportLength));
        }
        if (charsetIndex >= 0)
        {
            removals.Add((charsetIndex, charsetLength));
        }

        string remaining = ApplyRemovals(headInner, removals);

        StringBuilder sb = new(headInner.Length);
        // Insert charset first if present
        if (!string.IsNullOrEmpty(charsetTag))
        {
            sb.Append(charsetTag);
            if (remaining.Length > 0 && !remaining.StartsWith("\n", StringComparison.Ordinal))
            {
                sb.Append('\n');
            }
        }
        // Insert viewport next if present
        if (!string.IsNullOrEmpty(viewportTag))
        {
            sb.Append(viewportTag);
            if (remaining.Length > 0 && !remaining.StartsWith("\n", StringComparison.Ordinal) && sb.Length > 0)
            {
                sb.Append('\n');
            }
        }
        sb.Append(remaining);
        return sb.ToString();
    }
}
