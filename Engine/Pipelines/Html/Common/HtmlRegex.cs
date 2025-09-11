using System.Text.RegularExpressions;

namespace Engine.Pipelines.Html.Common;

public static partial class HtmlRegex
{
    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    public static partial Regex Comment();

    [GeneratedRegex(@"\s+")]
    public static partial Regex Whitespace();

    [GeneratedRegex(@"<script\s+src=""/refresh\.js""\s+async>\s*</script>")]
    public static partial Regex RefreshScript();

    [GeneratedRegex(@">\s+<")]
    public static partial Regex InterTagWhitespace();

    [GeneratedRegex(@"<head[^>]*>(.*?)</head>", RegexOptions.Singleline)]
    public static partial Regex HeadContent();

    [GeneratedRegex(@"</head>")]
    public static partial Regex CloseHeadTag();

    [GeneratedRegex(@"<main[^>]*>(.*?)</main>", RegexOptions.Singleline)]
    public static partial Regex MainContent();

    [GeneratedRegex(@"<main([^>]*)>\s*</main>", RegexOptions.Singleline)]
    public static partial Regex EmptyMain();
}
