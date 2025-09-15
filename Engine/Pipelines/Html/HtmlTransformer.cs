using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Engine.Pipelines.Core;

namespace Engine.Pipelines.Html;

public static class HtmlTransformer
{
    private static readonly IHtmlParser Parser = new HtmlParser();

    public static async Task<string> MergeTemplatesAsync(string appTemplate, string pageFragment)
    {
        ArgumentNullException.ThrowIfNull(appTemplate);
        ArgumentNullException.ThrowIfNull(pageFragment);

        using IHtmlDocument appDoc = await Parser.ParseDocumentAsync(appTemplate);
        using IHtmlDocument pageDoc = await Parser.ParseDocumentAsync(pageFragment);

        IHtmlHeadElement? appHead = appDoc.Head;
        IHtmlHeadElement? pageHead = pageDoc.Head;
        if (appHead != null && pageHead != null)
        {
            MergeHeadElements(appHead, pageHead);
        }

        IElement? appMain = appDoc.QuerySelector("main");
        IElement? pageMain = pageDoc.QuerySelector("main");
        if (appMain != null && pageMain != null)
        {
            appMain.InnerHtml = pageMain.InnerHtml;
        }

        return appDoc.DocumentElement.OuterHtml;
    }

    public static string MergeTemplates(string appTemplate, string pageFragment) => MergeTemplatesAsync(appTemplate, pageFragment).GetAwaiter().GetResult();

    private static void MergeHeadElements(IHtmlHeadElement appHead, IHtmlHeadElement pageHead)
    {
        Dictionary<string, IElement> appMetaTags = BuildMetaIndex(appHead);
        Dictionary<string, IElement> appLinkTags = BuildLinkIndex(appHead);

        foreach (IElement pageMeta in pageHead.QuerySelectorAll("meta"))
        {
            string? key = GetMetaKey(pageMeta);
            if (key != null && appMetaTags.TryGetValue(key, out IElement? existingMeta))
            {
                existingMeta.Replace(pageMeta.Clone());
            }
            else if (key == null || !appMetaTags.ContainsKey(key))
            {
                appHead.AppendChild(pageMeta.Clone());
            }
        }

        foreach (IElement pageLink in pageHead.QuerySelectorAll("link"))
        {
            string? key = GetLinkKey(pageLink);
            if (key != null && appLinkTags.TryGetValue(key, out IElement? existingLink))
            {
                existingLink.Replace(pageLink.Clone());
            }
            else if (key == null || !appLinkTags.ContainsKey(key))
            {
                appHead.AppendChild(pageLink.Clone());
            }
        }

        foreach (IElement element in pageHead.Children)
        {
            if (element.TagName is not "META" and not "LINK")
            {
                appHead.AppendChild(element.Clone());
            }
        }

        HtmlFormatter.OptimizeHeadOrder(appHead);
    }

    private static Dictionary<string, IElement> BuildMetaIndex(IHtmlHeadElement head)
    {
        Dictionary<string, IElement> index = new(StringComparer.OrdinalIgnoreCase);
        foreach (IElement meta in head.QuerySelectorAll("meta"))
        {
            string? key = GetMetaKey(meta);
            if (key != null)
            {
                index[key] = meta;
            }
        }

        return index;
    }

    private static Dictionary<string, IElement> BuildLinkIndex(IHtmlHeadElement head)
    {
        Dictionary<string, IElement> index = new(StringComparer.OrdinalIgnoreCase);
        foreach (IElement link in head.QuerySelectorAll("link"))
        {
            string? key = GetLinkKey(link);
            if (key != null)
            {
                index[key] = link;
            }
        }
        
        return index;
    }

    private static string? GetMetaKey(IElement meta)
    {
        if (meta.HasAttribute("charset"))
        {
            return "meta:charset";
        }
        if (meta.GetAttribute("http-equiv") is string httpEquiv && !string.IsNullOrEmpty(httpEquiv))
        {
            return $"meta:http-equiv:{httpEquiv.ToLowerInvariant()}";
        }
        if (meta.GetAttribute("name") is string name && !string.IsNullOrEmpty(name))
        {
            return $"meta:name:{name.ToLowerInvariant()}";
        }
        if (meta.GetAttribute("property") is string property && !string.IsNullOrEmpty(property))
        {
            return $"meta:property:{property.ToLowerInvariant()}";
        }
        
        return null;
    }

    private static string? GetLinkKey(IElement link)
    {
        string? rel = link.GetAttribute("rel");
        if (string.IsNullOrEmpty(rel))
        {
            return null;
        }

        string[] rels = rel.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (rels.Contains("canonical", StringComparer.OrdinalIgnoreCase))
        {
            return "link:rel:canonical";
        }
        if (rels.Contains("alternate", StringComparer.OrdinalIgnoreCase))
        {
            string? hreflang = link.GetAttribute("hreflang");
            if (!string.IsNullOrEmpty(hreflang))
            {
                return $"link:rel:alternate:{hreflang.ToLowerInvariant()}";
            }
        }

        return null;
    }

    public static async Task<string> RewriteAssetReferencesAsync(string html, AssetManifest manifest, string pageName)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        using IHtmlDocument doc = await Parser.ParseDocumentAsync(html);

        if (!string.IsNullOrWhiteSpace(manifest.Css))
        {
            string cssPath = $"/{Folders.Pages}/{pageName}/{manifest.Css}";
            RewriteStylesheetLinks(doc, $"{Files.Index}{FileExtensions.Css}", cssPath);
            RewriteStylesheetLinks(doc, $"{Files.Index}.module{FileExtensions.Css}", cssPath);
        }

        if (!string.IsNullOrWhiteSpace(manifest.Js))
        {
            string jsPath = $"/{Folders.Pages}/{pageName}/{manifest.Js}";
            RewriteScriptSources(doc, $"{Files.Index}{FileExtensions.Js}", jsPath);
        }

        return doc.DocumentElement.OuterHtml;
    }

    public static string RewriteAssetReferences(string html, AssetManifest manifest, string pageName) => RewriteAssetReferencesAsync(html, manifest, pageName).GetAwaiter().GetResult();

    private static void RewriteStylesheetLinks(IDocument doc, string oldPath, string newPath)
    {
        foreach (IElement link in doc.QuerySelectorAll($"link[href='{oldPath}'], link[href=\"{oldPath}\"]"))
        {
            link.SetAttribute("href", newPath);
        }
    }

    private static void RewriteScriptSources(IDocument doc, string oldPath, string newPath)
    {
        foreach (IElement script in doc.QuerySelectorAll($"script[src='{oldPath}'], script[src=\"{oldPath}\"]"))
        {
            script.SetAttribute("src", newPath);
        }
    }

    public static async Task<string> AddImageDimensionsAsync(string html, string pageBuildDirectory)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(pageBuildDirectory);

        using IHtmlDocument doc = await Parser.ParseDocumentAsync(html);

        string? pagesDir = Path.GetDirectoryName(pageBuildDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string? rootDir = pagesDir != null ? Path.GetDirectoryName(pagesDir) : null;

        foreach (IElement img in doc.QuerySelectorAll("img"))
        {
            if (img.HasAttribute("width") || img.HasAttribute("height"))
            {
                continue;
            }

            string? src = img.GetAttribute("src");
            if (string.IsNullOrEmpty(src) ||
                src.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                src.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? fullPath = null;
            try
            {
                if (src.StartsWith('/'))
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
                continue;
            }

            if (Images.ImageOptimizer.TryGetImageDimensions(fullPath, out int width, out int height))
            {
                img.SetAttribute("width", width.ToString(System.Globalization.CultureInfo.InvariantCulture));
                img.SetAttribute("height", height.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return doc.DocumentElement.OuterHtml;
    }

    public static string AddImageDimensions(string html, string pageBuildDirectory) => AddImageDimensionsAsync(html, pageBuildDirectory).GetAwaiter().GetResult();

    public static async Task<bool> HasHeadSectionAsync(string html)
    {
        using IHtmlDocument doc = await Parser.ParseDocumentAsync(html);
        return doc.Head != null;
    }

    public static bool HasHeadSection(string html) => HasHeadSectionAsync(html).GetAwaiter().GetResult();

    public static async Task<bool> HasMainSectionAsync(string html)
    {
        using IHtmlDocument doc = await Parser.ParseDocumentAsync(html);
        return doc.QuerySelector("main") != null;
    }

    public static bool HasMainSection(string html) => HasMainSectionAsync(html).GetAwaiter().GetResult();

    public static async Task<string> RemoveRefreshScriptAsync(string html)
    {
        using IHtmlDocument doc = await Parser.ParseDocumentAsync(html);
        IElement? refreshScript = doc.QuerySelector("script[src='/refresh.js']");
        refreshScript?.Remove();
        return doc.DocumentElement.OuterHtml;
    }

    public static string RemoveRefreshScript(string html) => RemoveRefreshScriptAsync(html).GetAwaiter().GetResult();
}
