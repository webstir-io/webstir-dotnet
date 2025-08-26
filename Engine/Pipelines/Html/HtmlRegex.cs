using System.Text.RegularExpressions;

namespace Engine.Pipelines.Html.Constants;

public static partial class HtmlRegex
{
    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    public static partial Regex Comment();
    
    [GeneratedRegex(@"\s+")]
    public static partial Regex Whitespace();
    
    [GeneratedRegex(@"<script\s+src=""/refresh\.js""\s+async>\s*</script>")]
    public static partial Regex RefreshScript();
}