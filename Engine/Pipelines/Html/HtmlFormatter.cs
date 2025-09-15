using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Engine.Pipelines.Html;

public static class HtmlFormatter
{
    private static readonly IHtmlParser Parser = new HtmlParser();
    private static readonly IMarkupFormatter Formatter = new PrettyMarkupFormatter();

    public static async Task<string> FormatHtmlAsync(string html)
    {
        using IHtmlDocument doc = await Parser.ParseDocumentAsync(html);

        IHtmlHeadElement? head = doc.Head;
        if (head != null)
        {
            OptimizeHeadOrder(head);
        }

        foreach (IElement style in doc.QuerySelectorAll("style"))
        {
            if (!string.IsNullOrEmpty(style.TextContent))
            {
                style.TextContent = style.TextContent.Trim();
            }
        }

        string formatted = doc.ToHtml(Formatter);

        // Remove the first level of indentation (tabs at the beginning of each line)
        string[] lines = formatted.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('\t'))
            {
                lines[i] = lines[i].Substring(1);
            }
        }

        formatted = string.Join('\n', lines);
        formatted = FixBlockElementTextFormatting(formatted);

        return formatted;
    }

    public static string FormatHtml(string html) => FormatHtmlAsync(html).GetAwaiter().GetResult();

    public static void OptimizeHeadOrder(IHtmlHeadElement head)
    {
        ArgumentNullException.ThrowIfNull(head);

        List<IElement> elements = [];
        while (head.FirstChild != null)
        {
            if (head.FirstChild is IElement element)
            {
                elements.Add(element);
            }
            head.RemoveChild(head.FirstChild);
        }

        List<IElement> sortedElements = elements.OrderBy(GetElementPriority).ToList();
        foreach (IElement element in sortedElements)
        {
            head.AppendChild(element);
        }
    }

    private static int GetElementPriority(IElement element)
    {
        string tagName = element.TagName.ToUpperInvariant();

        switch (tagName)
        {
            case "META":
                if (element.HasAttribute("charset"))
                {
                    return 1;
                }
                string? name = element.GetAttribute("name");
                if (string.Equals(name, "viewport", StringComparison.OrdinalIgnoreCase))
                {
                    return 2;
                }
                return 4;

            case "TITLE":
                return 3;

            case "LINK":
                string? rel = element.GetAttribute("rel");
                if (rel != null)
                {
                    if (rel.Contains("preload", StringComparison.OrdinalIgnoreCase) ||
                        rel.Contains("modulepreload", StringComparison.OrdinalIgnoreCase) ||
                        rel.Contains("prefetch", StringComparison.OrdinalIgnoreCase) ||
                        rel.Contains("dns-prefetch", StringComparison.OrdinalIgnoreCase) ||
                        rel.Contains("preconnect", StringComparison.OrdinalIgnoreCase))
                    {
                        return 5;
                    }
                    if (rel.Contains("stylesheet", StringComparison.OrdinalIgnoreCase))
                    {
                        return 6;
                    }
                }
                // Other link tags
                return 5;

            case "STYLE":
                return 7;

            case "SCRIPT":
                return 8;

            default:
                return 9;
        }
    }


    private static string FixBlockElementTextFormatting(string html)
    {
        string[] blockElements = ["main", "header", "footer", "section", "article", "nav", "aside", "div", "h1", "h2", "h3", "h4", "h5", "h6", "p"];

        foreach (string element in blockElements)
        {
            string pattern = $@"(<{element}>)([^<]+)(</{element}>)";
            html = Regex.Replace(html, pattern, m =>
            {
                string openTag = m.Groups[1].Value;
                string content = m.Groups[2].Value.Trim();
                string closeTag = m.Groups[3].Value;

                if (string.IsNullOrWhiteSpace(content))
                {
                    return m.Value;
                }

                int lineStart = html.LastIndexOf('\n', m.Index) + 1;
                string linePrefix = html[lineStart..m.Index];
                string indentation = HtmlRegex.Indentation().Match(linePrefix).Value;
                string contentIndentation = indentation + "\t";

                return $"{openTag}\n{contentIndentation}{content}\n{indentation}{closeTag}";
            }, RegexOptions.Multiline);
        }

        return html;
    }
}
