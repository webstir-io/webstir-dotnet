using System.IO;
using System.Text.RegularExpressions;
using Engine.Pipelines.Html.Constants;

namespace Engine.Pipelines.Html.Models;

public sealed class HtmlFile(string filepath)
{
    private string html = File.ReadAllText(filepath);

    public string Html => html;

    public string Merge(string pageHtml)
    {
        string result = html;

        Match headContentMatch = HtmlRegex.HeadContent().Match(pageHtml);

        if (headContentMatch.Success)
        {
            string headContent = headContentMatch.Groups[1].Value.Trim();
            result = HtmlRegex.CloseHeadTag().Replace(result, $"{headContent}\n</head>");
        }

        Match mainContentMatch = HtmlRegex.MainContent().Match(pageHtml);

        if (mainContentMatch.Success)
        {
            string mainContent = mainContentMatch.Groups[1].Value.Trim();
            result = HtmlRegex.EmptyMain().Replace(result, $"<main$1>{mainContent}</main>");
        }

        return result;
    }

    public void Remove(string markup) => html = html.Replace(markup, string.Empty);
}
