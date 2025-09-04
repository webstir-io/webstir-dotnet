using System;
using System.IO;
using Engine.Pipelines.Html.Constants;

namespace Engine.Models;

public class HtmlFile(string _filepath)
{
    private string _html = File.ReadAllText(_filepath);

    public string Html => _html;

    public string Merge(string pageHtml)
    {
        string result = _html;

        // Extract head content from the page
        System.Text.RegularExpressions.Match headContentMatch = HtmlRegex.HeadContent().Match(pageHtml);

        if (headContentMatch.Success)
        {
            string headContent = headContentMatch.Groups[1].Value.Trim();
            // Insert head content before closing </head> tag in template
            result = HtmlRegex.CloseHeadTag().Replace(result, headContent + "\n</head>");
        }

        // Extract main content from the page
        System.Text.RegularExpressions.Match mainContentMatch = HtmlRegex.MainContent().Match(pageHtml);

        if (mainContentMatch.Success)
        {
            string mainContent = mainContentMatch.Groups[1].Value.Trim();
            // Replace <main> </main> with <main>content</main>
            result = HtmlRegex.EmptyMain().Replace(result, "<main$1>" + mainContent + "</main>");
        }

        return result;
    }

    public void Remove(string markup) => _html = _html.Replace(markup, string.Empty);

    private string GetTagContent(string tagName, string html)
    {
        string startTag = ToStartTag(tagName);
        string endTag = ToEndTag(tagName);
        int contentStart = html.IndexOf(startTag, StringComparison.Ordinal) + startTag.Length;
        int contentEnd = html.IndexOf(endTag, StringComparison.Ordinal);
        int contentLength = contentEnd - contentStart - 1;
        return html.Substring(contentStart, contentLength);
    }

    private static string ToStartTag(string tagName) => $"<{tagName}>";
    private static string ToEndTag(string tagName) => $"</{tagName}>";
}
