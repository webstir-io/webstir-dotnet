namespace Engine.Models;

public class HtmlFile(string _filepath)
{
    private string _html = File.ReadAllText(_filepath);

    public string Html => _html;
    
    public string Merge(string pageHtml)
    {
        var result = _html;
        
        // Extract head content from the page
        var headContentMatch = System.Text.RegularExpressions.Regex.Match(
            pageHtml, 
            @"<head[^>]*>(.*?)</head>", 
            System.Text.RegularExpressions.RegexOptions.Singleline
        );
        
        if (headContentMatch.Success)
        {
            var headContent = headContentMatch.Groups[1].Value.Trim();
            // Insert head content before closing </head> tag in template
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"</head>",
                headContent + "\n</head>"
            );
        }
        
        // Extract main content from the page
        var mainContentMatch = System.Text.RegularExpressions.Regex.Match(
            pageHtml, 
            @"<main[^>]*>(.*?)</main>", 
            System.Text.RegularExpressions.RegexOptions.Singleline
        );
        
        if (mainContentMatch.Success)
        {
            var mainContent = mainContentMatch.Groups[1].Value.Trim();
            // Replace <main> </main> with <main>content</main>
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"<main([^>]*)>\s*</main>",
                "<main$1>" + mainContent + "</main>"
            );
        }
        
        return result;
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