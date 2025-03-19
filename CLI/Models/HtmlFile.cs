namespace CLI.Models;

public class HtmlFile(string _filepath)
{
    private string _html = File.ReadAllText(_filepath);

    public string Html => _html;
    
    public string Merge(string html)
    {
        var headTag = "head";
        var headContent = GetTagContent(headTag, html);
        var headInsertLocation = _html.IndexOf(ToEndTag(headTag)) - 1;
        var mergedHtml = _html.Insert(headInsertLocation, headContent);

        var mainTag = "main";
        var mainContent = GetTagContent(mainTag, html);
        var mainInsertLocation = mergedHtml.IndexOf(ToEndTag(mainTag)) - 1;
        mergedHtml = mergedHtml.Insert(mainInsertLocation, mainContent);

        return mergedHtml;
    }

    public void Remove(string markup)
    {
        _html = _html.Replace(markup, string.Empty);
    }

    private string GetTagContent(string tagName, string html)
    {
        var startTag = ToStartTag(tagName);
        var endTag = ToEndTag(tagName);
        var contentStart = html.IndexOf(startTag) + startTag.Length;
        var contentEnd = html.IndexOf(endTag);
        var contentLength = contentEnd - contentStart - 1;
        return html.Substring(contentStart, contentLength);
    }

    private static string ToStartTag(string tagName) => $"<{tagName}>";
    private static string ToEndTag(string tagName) => $"</{tagName}>";
}