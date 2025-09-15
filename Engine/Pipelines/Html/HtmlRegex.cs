using System.Text.RegularExpressions;

namespace Engine.Pipelines.Html;

public static partial class HtmlRegex
{
    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    public static partial Regex Comment();

    [GeneratedRegex(@"<script\s+src=""/refresh\.js""\s+async>\s*</script>")]
    public static partial Regex RefreshScript();

    [GeneratedRegex(@"<head[^>]*>(.*?)</head>", RegexOptions.Singleline)]
    public static partial Regex HeadContent();

    [GeneratedRegex(@"</head>")]
    public static partial Regex CloseHeadTag();

    [GeneratedRegex(@"<main[^>]*>(.*?)</main>", RegexOptions.Singleline)]
    public static partial Regex MainContent();

    [GeneratedRegex(@"<main([^>]*)>\s*</main>", RegexOptions.Singleline)]
    public static partial Regex EmptyMain();

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    public static partial Regex ImgTag();

    [GeneratedRegex(@"\bwidth\s*=", RegexOptions.IgnoreCase)]
    public static partial Regex WidthAttr();

    [GeneratedRegex(@"\bheight\s*=", RegexOptions.IgnoreCase)]
    public static partial Regex HeightAttr();

    [GeneratedRegex(@"\bsrc\s*=\s*(['""])(?<src>.*?)\1", RegexOptions.IgnoreCase)]
    public static partial Regex ImgSrc();

    [GeneratedRegex(@"<script\b(?<attrs>[^>]*?)src=""(?<url>https?://[^""]+)""(?<tail>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    public static partial Regex ExternalScriptTag();

    [GeneratedRegex(@"<link\b(?<attrs>[^>]*?)rel=""stylesheet""(?<mid>[^>]*?)href=""(?<url>https?://[^""]+)""(?<tail>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    public static partial Regex ExternalStylesheetLink();

    [GeneratedRegex(@"\bintegrity=", RegexOptions.IgnoreCase)]
    public static partial Regex IntegrityAttr();

    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    public static partial Regex MetaTag();

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    public static partial Regex LinkTag();

    [GeneratedRegex(@"<a\b[^>]*href=""(?<href>[^\""]+)""[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    public static partial Regex AnchorHref();
}
