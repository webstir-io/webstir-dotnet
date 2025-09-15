using System.Text.RegularExpressions;

namespace Engine.Pipelines.Html;

public static partial class HtmlRegex
{
    [GeneratedRegex(@"<script\b(?<attrs>[^>]*?)src=""(?<url>https?://[^""]+)""(?<tail>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    public static partial Regex ExternalScriptTag();

    [GeneratedRegex(@"<link\b(?<attrs>[^>]*?)rel=""stylesheet""(?<mid>[^>]*?)href=""(?<url>https?://[^""]+)""(?<tail>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    public static partial Regex ExternalStylesheetLink();

    [GeneratedRegex(@"\bintegrity=", RegexOptions.IgnoreCase)]
    public static partial Regex IntegrityAttr();

    [GeneratedRegex(@"<a\b[^>]*href=""(?<href>[^\""]+)""[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    public static partial Regex AnchorHref();

    [GeneratedRegex(@"^\s*")]
    public static partial Regex Indentation();
}
