namespace Engine.Models;

public class HtmlFile(string _filepath)
{
    private string _html = File.ReadAllText(_filepath);

    public string Html => _html;
    
    public string Merge(string pageHtml)
    {
        string result = _html;
        
        // Extract head content from the page
        System.Text.RegularExpressions.Match headContentMatch = System.Text.RegularExpressions.Regex.Match(
            pageHtml, 
            @"<head[^>]*>(.*?)</head>", 
            System.Text.RegularExpressions.RegexOptions.Singleline
        );
        
        if (headContentMatch.Success)
        {
            string headContent = headContentMatch.Groups[1].Value.Trim();
            // Insert head content before closing </head> tag in template
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"</head>",
                headContent + "\n</head>"
            );
        }
        
        // Extract main content from the page
        System.Text.RegularExpressions.Match mainContentMatch = System.Text.RegularExpressions.Regex.Match(
            pageHtml, 
            @"<main[^>]*>(.*?)</main>", 
            System.Text.RegularExpressions.RegexOptions.Singleline
        );
        
        if (mainContentMatch.Success)
        {
            string mainContent = mainContentMatch.Groups[1].Value.Trim();
            // Replace <main> </main> with <main>content</main>
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"<main([^>]*)>\s*</main>",
                "<main$1>" + mainContent + "</main>"
            );
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
