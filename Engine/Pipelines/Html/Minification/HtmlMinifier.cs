using System;
using System.Collections.Generic;
using System.Text;

namespace Engine.Pipelines.Html.Minification;

public static class HtmlMinifier
{
    public static string Minify(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        if (html.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder output = new(html.Length);

        int index = 0;
        int length = html.Length;

        // Track context for whitespace/comment handling
        int scriptDepth = 0;
        int styleDepth = 0;
        int preDepth = 0;
        int codeDepth = 0;
        int textareaDepth = 0;

        Stack<string> elementStack = new();

        while (index < length)
        {
            char currentChar = html[index];

            if (currentChar == '<')
            {
                // HTML comment: remove entirely (outside raw-text elements)
                if (IsAt(html, index, "<!--") && scriptDepth == 0 && styleDepth == 0 && preDepth == 0 && codeDepth == 0 && textareaDepth == 0)
                {
                    index = SkipUntil(html, index + 4, "-->");
                    continue;
                }

                // CDATA (copy through)
                if (IsAt(html, index, "<![CDATA["))
                {
                    int cdataEnd = IndexOf(html, index + 9, "]]>");
                    if (cdataEnd < 0)
                    {
                        // Unterminated; emit the rest
                        output.Append(html.AsSpan(index));
                        break;
                    }
                    output.Append(html.AsSpan(index, cdataEnd + 3 - index));
                    index = cdataEnd + 3;
                    continue;
                }

                // Doctype or other declarations (emit as-is until '>')
                if (IsAt(html, index, "<!"))
                {
                    int declEnd = IndexOfChar(html, index, '>');
                    declEnd = declEnd < 0 ? length - 1 : declEnd;
                    output.Append(html.AsSpan(index, declEnd - index + 1));
                    index = declEnd + 1;
                    continue;
                }

                // Normal tag
                int tagStart = index;
                index++;
                bool isClosing = false;
                if (index < length && html[index] == '/')
                {
                    isClosing = true;
                    index++;
                }

                int nameStart = index;
                while (index < length && IsTagNameChar(html[index]))
                {
                    index++;
                }
                string tagName = html[nameStart..Math.Min(index, length)];
                string tagNameLower = tagName.ToLowerInvariant();

                // Read attributes region raw
                List<HtmlAttribute> attributes = [];
                SkipAsciiWhitespace(html, ref index);
                bool selfClosing = false;

                while (index < length && html[index] != '>')
                {
                    if (html[index] == '/')
                    {
                        selfClosing = true;
                        index++;
                        SkipAsciiWhitespace(html, ref index);
                        continue;
                    }

                    // Attribute name
                    int attrNameStart = index;
                    while (index < length && IsAttributeNameChar(html[index]))
                    {
                        index++;
                    }
                    if (attrNameStart == index)
                    {
                        // Fallback: skip one character to avoid infinite loop
                        index++;
                        continue;
                    }
                    string attrNameRaw = html[attrNameStart..index];
                    string attrNameLower = attrNameRaw.ToLowerInvariant();

                    SkipAsciiWhitespace(html, ref index);

                    string? attrValue = null;
                    bool hasValue = false;
                    if (index < length && html[index] == '=')
                    {
                        hasValue = true;
                        index++;
                        SkipAsciiWhitespace(html, ref index);

                        if (index < length && (html[index] == '\'' || html[index] == '"'))
                        {
                            char quoteChar = html[index];
                            index++;
                            int valueStart = index;
                            while (index < length && html[index] != quoteChar)
                            {
                                index++;
                            }
                            attrValue = html[valueStart..Math.Min(index, length)];
                            if (index < length && html[index] == quoteChar)
                            {
                                index++;
                            }
                        }
                        else
                        {
                            int valueStart = index;
                            while (index < length && !IsAsciiWhitespace(html[index]) && html[index] != '>' && html[index] != '/')
                            {
                                index++;
                            }
                            attrValue = html[valueStart..Math.Min(index, length)];
                        }
                    }

                    attributes.Add(new HtmlAttribute(attrNameRaw, attrNameLower, attrValue, hasValue));
                    SkipAsciiWhitespace(html, ref index);
                }

                // Consume '>'
                if (index < length && html[index] == '>')
                {
                    index++;
                }

                // Update context stacks
                if (isClosing)
                {
                    if (elementStack.Count > 0)
                    {
                        string topElement = elementStack.Peek();
                        if (topElement == tagNameLower)
                        {
                            elementStack.Pop();
                            if (tagNameLower == "script")
                            {
                                scriptDepth = Math.Max(0, scriptDepth - 1);
                            }
                            if (tagNameLower == "style")
                            {
                                styleDepth = Math.Max(0, styleDepth - 1);
                            }
                            if (tagNameLower == "pre")
                            {
                                preDepth = Math.Max(0, preDepth - 1);
                            }
                            if (tagNameLower == "code")
                            {
                                codeDepth = Math.Max(0, codeDepth - 1);
                            }
                            if (tagNameLower == "textarea")
                            {
                                textareaDepth = Math.Max(0, textareaDepth - 1);
                            }
                        }
                    }

                    // Emit closing tag unchanged (preserve original case)
                    output.Append('<').Append('/').Append(tagName).Append('>');
                    continue;
                }
                else
                {
                    bool isVoid = IsVoidElement(tagNameLower);
                    if (!selfClosing && !isVoid)
                    {
                        elementStack.Push(tagNameLower);
                        if (tagNameLower == "script")
                        {
                            scriptDepth++;
                        }
                        if (tagNameLower == "style")
                        {
                            styleDepth++;
                        }
                        if (tagNameLower == "pre")
                        {
                            preDepth++;
                        }
                        if (tagNameLower == "code")
                        {
                            codeDepth++;
                        }
                        if (tagNameLower == "textarea")
                        {
                            textareaDepth++;
                        }
                    }

                    // Transform attributes (safe-only)
                    List<HtmlAttribute> transformed = [];
                    for (int attributeIndex = 0; attributeIndex < attributes.Count; attributeIndex++)
                    {
                        HtmlAttribute attribute = attributes[attributeIndex];

                        // Drop default type on <script>/<style>
                        if (attribute.NameLower == "type" && attribute.HasValue && attribute.Value != null)
                        {
                            if (tagNameLower == "script" && attribute.Value.Equals("text/javascript", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            if (tagNameLower == "style" && attribute.Value.Equals("text/css", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }

                        // Collapse boolean attributes
                        if (IsBooleanAttribute(attribute.NameLower, tagNameLower))
                        {
                            transformed.Add(new HtmlAttribute(attribute.NameRaw, attribute.NameLower, null, false));
                            continue;
                        }

                        transformed.Add(attribute);
                    }

                    // Serialize opening tag
                    output.Append('<').Append(tagName);
                    for (int attributeIndex = 0; attributeIndex < transformed.Count; attributeIndex++)
                    {
                        HtmlAttribute attribute = transformed[attributeIndex];
                        output.Append(' ').Append(attribute.NameRaw);
                        if (attribute.HasValue && attribute.Value != null)
                        {
                            output.Append('=');
                            if (IsUnquotedAttributeValueSafe(attribute.Value))
                            {
                                output.Append(attribute.Value);
                            }
                            else
                            {
                                // Use double quotes for consistency
                                output.Append('"').Append(attribute.Value).Append('"');
                            }
                        }
                    }

                    if (selfClosing)
                    {
                        output.Append("/>");
                    }
                    else
                    {
                        output.Append('>');
                    }

                    continue;
                }
            }

            // Text node
            int textStart = index;
            while (index < length && html[index] != '<')
            {
                index++;
            }
            string textSegment = html[textStart..index];

            if (scriptDepth > 0 || styleDepth > 0 || preDepth > 0 || codeDepth > 0 || textareaDepth > 0)
            {
                // Preserve as-is inside raw/sensitive elements
                output.Append(textSegment);
            }
            else
            {
                if (!IsAllWhitespace(textSegment))
                {
                    // Non-whitespace text content: keep as-is
                    output.Append(textSegment);
                }
                // Whitespace-only between tags is dropped entirely (collapses inter-tag whitespace)
            }
        }

        return output.ToString().Trim();
    }

    private static bool IsAt(string text, int index, string value)
    {
        int remaining = text.Length - index;
        if (remaining < value.Length)
        {
            return false;
        }
        for (int checkIndex = 0; checkIndex < value.Length; checkIndex++)
        {
            if (text[index + checkIndex] != value[checkIndex])
            {
                return false;
            }
        }
        return true;
    }

    private static int SkipUntil(string text, int startIndex, string terminator)
    {
        int endIndex = IndexOf(text, startIndex, terminator);
        if (endIndex < 0)
        {
            return text.Length;
        }
        return endIndex + terminator.Length;
    }

    private static int IndexOf(string text, int startIndex, string value)
    {
        int lastStart = text.Length - value.Length;
        for (int searchIndex = startIndex; searchIndex <= lastStart; searchIndex++)
        {
            bool match = true;
            for (int valueIndex = 0; valueIndex < value.Length; valueIndex++)
            {
                if (text[searchIndex + valueIndex] != value[valueIndex])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                return searchIndex;
            }
        }
        return -1;
    }

    private static int IndexOfChar(string text, int startIndex, char character)
    {
        for (int searchIndex = startIndex; searchIndex < text.Length; searchIndex++)
        {
            if (text[searchIndex] == character)
            {
                return searchIndex;
            }
        }
        return -1;
    }

    private static void SkipAsciiWhitespace(string text, ref int index)
    {
        while (index < text.Length && IsAsciiWhitespace(text[index]))
        {
            index++;
        }
    }

    private static bool IsAsciiWhitespace(char character) => character is ' ' or '\t' or '\n' or '\r' or '\f' or '\v';

    private static bool IsAllWhitespace(string text)
    {
        for (int textIndex = 0; textIndex < text.Length; textIndex++)
        {
            if (!IsAsciiWhitespace(text[textIndex]))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsTagNameChar(char character) => char.IsLetterOrDigit(character) || character is ':' or '-' or '_';

    private static bool IsAttributeNameChar(char character)
    {
        // Conservative: stop on whitespace, '=', '/', '>'
        if (IsAsciiWhitespace(character))
        {
            return false;
        }
        return character is not '=' and not '/' and not '>';
    }

    private static bool IsUnquotedAttributeValueSafe(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        for (int valueIndex = 0; valueIndex < value.Length; valueIndex++)
        {
            char character = value[valueIndex];
            if (IsAsciiWhitespace(character))
            {
                return false;
            }
            if (character is '"' or '\'' or '`' or '=' or '<' or '>')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsVoidElement(string tagNameLower)
    {
        return tagNameLower is "area"
            or "base"
            or "br"
            or "col"
            or "embed"
            or "hr"
            or "img"
            or "input"
            or "link"
            or "meta"
            or "source"
            or "track"
            or "wbr";
    }

    private static bool IsBooleanAttribute(string attributeNameLower, string tagNameLower)
    {
        // Common HTML boolean attributes. Some are element-specific but safe to treat as boolean by name.
        return attributeNameLower is "allowfullscreen"
            or "async"
            or "autofocus"
            or "autoplay"
            or "checked"
            or "controls"
            or "default"
            or "defer"
            or "disabled"
            or "formnovalidate"
            or "hidden"
            or "inert"
            or "ismap"
            or "itemscope"
            or "loop"
            or "multiple"
            or "muted"
            or "nomodule"
            or "novalidate"
            or "open"
            or "playsinline"
            or "readonly"
            or "required"
            or "reversed"
            or "selected"
            or "truespeed";
    }

    private readonly record struct HtmlAttribute(string NameRaw, string NameLower, string? Value, bool HasValue);
}

