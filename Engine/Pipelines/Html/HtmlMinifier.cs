using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Engine.Pipelines.Html;

public static class HtmlMinifier
{
    private static readonly HashSet<string> VoidElements = new()
    {
        "area", "base", "br", "col", "embed", "hr", "img",
        "input", "link", "meta", "source", "track", "wbr"
    };

    private static readonly HashSet<string> PreserveWhitespaceElements = new()
    {
        "script", "style", "pre", "code", "textarea"
    };

    private static readonly HashSet<string> BooleanAttributes = new()
    {
        "allowfullscreen", "async", "autofocus", "autoplay", "checked",
        "controls", "default", "defer", "disabled", "formnovalidate",
        "hidden", "inert", "ismap", "itemscope", "loop", "multiple",
        "muted", "nomodule", "novalidate", "open", "playsinline",
        "readonly", "required", "reversed", "selected", "truespeed"
    };

    public static string Minify(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        if (html.Length == 0)
        {
            return string.Empty;
        }

        MinificationContext context = new(html);
        StringBuilder result = new(html.Length);

        while (!context.IsAtEnd)
        {
            if (context.Current == '<')
            {
                ProcessTag(context, result);
            }
            else
            {
                ProcessText(context, result);
            }
        }

        return result.ToString().Trim();
    }

    private static void ProcessTag(MinificationContext context, StringBuilder result)
    {
        if (context.StartsWith("<!--"))
        {
            ProcessComment(context, result);
            return;
        }

        if (context.StartsWith("<![CDATA["))
        {
            ProcessCData(context, result);
            return;
        }

        if (context.StartsWith("<!"))
        {
            ProcessDeclaration(context, result);
            return;
        }

        ProcessHtmlTag(context, result);
    }

    private static void ProcessComment(MinificationContext context, StringBuilder result)
    {
        // Remove comments unless inside preserved elements
        if (!context.IsInsidePreservedElement)
        {
            context.SkipUntil("-->");
        }
        else
        {
            int start = context.Position;
            context.SkipUntil("-->");
            result.Append(context.GetText(start, context.Position));
        }
    }

    private static void ProcessCData(MinificationContext context, StringBuilder result)
    {
        int start = context.Position;
        context.SkipUntil("]]>");
        result.Append(context.GetText(start, context.Position));
    }

    private static void ProcessDeclaration(MinificationContext context, StringBuilder result)
    {
        int start = context.Position;
        context.SkipToChar('>');
        context.Advance(); // Include '>'
        result.Append(context.GetText(start, context.Position));
    }

    private static void ProcessHtmlTag(MinificationContext context, StringBuilder result)
    {
        context.Advance(); // Skip '<'

        bool isClosing = false;
        if (context.Current == '/')
        {
            isClosing = true;
            context.Advance();
        }

        string tagName = context.ReadTagName();
        string tagNameLower = tagName.ToLowerInvariant();

        if (isClosing)
        {
            ProcessClosingTag(context, result, tagName, tagNameLower);
        }
        else
        {
            ProcessOpeningTag(context, result, tagName, tagNameLower);
        }
    }

    private static void ProcessClosingTag(MinificationContext context, StringBuilder result, string tagName, string tagNameLower)
    {
        context.UpdateElementStack(tagNameLower, isClosing: true);
        context.SkipWhitespace();
        context.SkipToChar('>');
        context.Advance(); // Skip '>'

        result.Append("</").Append(tagName).Append('>');
    }

    private static void ProcessOpeningTag(MinificationContext context, StringBuilder result, string tagName, string tagNameLower)
    {
        List<HtmlAttribute> attributes = ParseAttributes(context);
        bool selfClosing = context.CheckSelfClosing();

        context.SkipToChar('>');
        context.Advance(); // Skip '>'

        bool isVoid = VoidElements.Contains(tagNameLower);
        if (!selfClosing && !isVoid)
        {
            context.UpdateElementStack(tagNameLower, isClosing: false);
        }

        WriteOpeningTag(result, tagName, tagNameLower, attributes, selfClosing);
    }

    private static List<HtmlAttribute> ParseAttributes(MinificationContext context)
    {
        List<HtmlAttribute> attributes = new();
        context.SkipWhitespace();

        while (!context.IsAtEnd && context.Current != '>' && context.Current != '/')
        {
            string attrName = context.ReadAttributeName();
            if (string.IsNullOrEmpty(attrName))
            {
                context.Advance(); // Skip invalid character
                continue;
            }

            context.SkipWhitespace();

            string? attrValue = null;
            bool hasValue = false;

            if (context.Current == '=')
            {
                hasValue = true;
                context.Advance();
                context.SkipWhitespace();
                attrValue = context.ReadAttributeValue();
            }

            attributes.Add(new HtmlAttribute(attrName, attrName.ToLowerInvariant(), attrValue, hasValue));
            context.SkipWhitespace();
        }

        return attributes;
    }

    private static void WriteOpeningTag(StringBuilder result, string tagName, string tagNameLower,
        List<HtmlAttribute> attributes, bool selfClosing)
    {
        result.Append('<').Append(tagName);

        foreach (HtmlAttribute attr in TransformAttributes(attributes, tagNameLower))
        {
            result.Append(' ').Append(attr.NameRaw);

            if (attr.HasValue && attr.Value != null)
            {
                result.Append('=');
                if (IsUnquotedAttributeValueSafe(attr.Value))
                {
                    result.Append(attr.Value);
                }
                else
                {
                    result.Append('"').Append(attr.Value).Append('"');
                }
            }
        }

        result.Append(selfClosing ? "/>" : ">");
    }

    private static IEnumerable<HtmlAttribute> TransformAttributes(List<HtmlAttribute> attributes, string tagNameLower)
    {
        foreach (HtmlAttribute attr in attributes)
        {
            // Drop default type attributes
            if (attr.NameLower == "type" && attr.HasValue && attr.Value != null)
            {
                if (tagNameLower == "script" && attr.Value.Equals("text/javascript", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (tagNameLower == "style" && attr.Value.Equals("text/css", StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Collapse boolean attributes
            if (BooleanAttributes.Contains(attr.NameLower))
            {
                yield return new HtmlAttribute(attr.NameRaw, attr.NameLower, null, false);
                continue;
            }

            yield return attr;
        }
    }

    private static void ProcessText(MinificationContext context, StringBuilder result)
    {
        int start = context.Position;
        while (!context.IsAtEnd && context.Current != '<')
        {
            context.Advance();
        }

        string text = context.GetText(start, context.Position);

        if (context.IsInsidePreservedElement)
        {
            result.Append(text);
        }
        else if (!IsAllWhitespace(text))
        {
            result.Append(text);
        }
        // Drop whitespace-only text between tags
    }

    private static bool IsUnquotedAttributeValueSafe(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c) || c is '"' or '\'' or '`' or '=' or '<' or '>')
                return false;
        }
        return true;
    }

    private static bool IsAllWhitespace(string text)
    {
        foreach (char c in text)
        {
            if (!char.IsWhiteSpace(c))
                return false;
        }
        return true;
    }

    private readonly record struct HtmlAttribute(string NameRaw, string NameLower, string? Value, bool HasValue);

    private sealed class MinificationContext
    {
        private readonly string _html;
        private readonly Stack<string> _elementStack = new();
        private readonly Dictionary<string, int> _elementDepths = new();

        public int Position
        {
            get; private set;
        }
        public char Current => Position < _html.Length ? _html[Position] : '\0';
        public bool IsAtEnd => Position >= _html.Length;

        public bool IsInsidePreservedElement =>
            PreserveWhitespaceElements.Any(elem => _elementDepths.GetValueOrDefault(elem, 0) > 0);

        public MinificationContext(string html)
        {
            _html = html;
            Position = 0;

            foreach (string elem in PreserveWhitespaceElements)
            {
                _elementDepths[elem] = 0;
            }
        }

        public void Advance() => Position++;

        public bool StartsWith(string text)
        {
            if (Position + text.Length > _html.Length)
                return false;

            return _html.AsSpan(Position, text.Length).SequenceEqual(text);
        }

        public void SkipUntil(string terminator)
        {
            int index = _html.IndexOf(terminator, Position, StringComparison.Ordinal);
            Position = index >= 0 ? index + terminator.Length : _html.Length;
        }

        public void SkipToChar(char c)
        {
            while (!IsAtEnd && Current != c)
                Advance();
        }

        public void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(Current))
                Advance();
        }

        public string GetText(int start, int end) =>
            _html[start..Math.Min(end, _html.Length)];

        public string ReadTagName()
        {
            int start = Position;
            while (!IsAtEnd && (char.IsLetterOrDigit(Current) || Current is ':' or '-' or '_'))
                Advance();
            return GetText(start, Position);
        }

        public string ReadAttributeName()
        {
            int start = Position;
            while (!IsAtEnd && !char.IsWhiteSpace(Current) && Current is not '=' and not '/' and not '>')
                Advance();
            return GetText(start, Position);
        }

        public string? ReadAttributeValue()
        {
            if (Current is '\'' or '"')
            {
                char quote = Current;
                Advance();
                int start = Position;
                while (!IsAtEnd && Current != quote)
                    Advance();
                string value = GetText(start, Position);
                if (Current == quote)
                    Advance();
                return value;
            }

            int valueStart = Position;
            while (!IsAtEnd && !char.IsWhiteSpace(Current) && Current is not '>' and not '/')
                Advance();
            return GetText(valueStart, Position);
        }

        public bool CheckSelfClosing()
        {
            bool selfClosing = false;
            while (!IsAtEnd && Current == '/')
            {
                selfClosing = true;
                Advance();
                SkipWhitespace();
            }
            return selfClosing;
        }

        public void UpdateElementStack(string tagNameLower, bool isClosing)
        {
            if (isClosing)
            {
                if (_elementStack.Count > 0 && _elementStack.Peek() == tagNameLower)
                {
                    _elementStack.Pop();
                    if (_elementDepths.TryGetValue(tagNameLower, out int depth))
                    {
                        _elementDepths[tagNameLower] = Math.Max(0, depth - 1);
                    }
                }
            }
            else
            {
                _elementStack.Push(tagNameLower);
                if (_elementDepths.TryGetValue(tagNameLower, out int depth))
                {
                    _elementDepths[tagNameLower] = depth + 1;
                }
            }
        }
    }
}
