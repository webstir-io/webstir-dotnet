using System.Text.RegularExpressions;

namespace Engine.Pipelines.Html.Parsing;

public static class HtmlParser
{
    public static (string? headContent, string? mainContent) ExtractSections(string html)
    {
        string? headContent = null;
        string? mainContent = null;

        Match headMatch = HtmlRegex.HeadContent().Match(html);
        if (headMatch.Success)
        {
            headContent = headMatch.Groups[1].Value.Trim();
        }

        Match mainMatch = HtmlRegex.MainContent().Match(html);
        if (mainMatch.Success)
        {
            mainContent = mainMatch.Groups[1].Value.Trim();
        }

        return (headContent, mainContent);
    }

    public static bool HasHeadSection(string html) => HtmlRegex.HeadContent().IsMatch(html);

    public static bool HasMainSection(string html) => HtmlRegex.MainContent().IsMatch(html);

    public static string RemoveComments(string html) => HtmlRegex.Comment().Replace(html, string.Empty);

    public static string RemoveRefreshScript(string html) => HtmlRegex.RefreshScript().Replace(html, string.Empty);
}
