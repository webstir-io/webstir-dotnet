using System;

namespace Engine.Extensions;

public static class TextScanner
{
    // Scans forward from startIndex over ASCII whitespace. Returns whether a newline
    // was seen, the next non-whitespace character (or '\0'), and the index after
    // the last whitespace character.
    public static (bool sawNewline, char nextNonWhitespace, int nextIndex) ScanAsciiWhitespace(string text, int startIndex)
    {
        ArgumentNullException.ThrowIfNull(text);

        bool sawNewline = false;
        int length = text.Length;
        int cursor = startIndex;
        while (cursor < length && text[cursor].IsAsciiWhitespace())
        {
            if (text[cursor].IsLineTerminator())
            {
                sawNewline = true;
            }
            cursor++;
        }

        char nextNonWhitespace = cursor < length ? text[cursor] : '\0';
        return (sawNewline, nextNonWhitespace, cursor);
    }

    // Emits a backslash escape sequence starting at index if present.
    // Returns true if an escape was emitted and advances index accordingly.
    public static bool TryEmitEscape(string text, ref int index, Action<char> emit)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(emit);

        if (index >= text.Length || text[index] != '\\')
        {
            return false;
        }

        emit('\\');
        index++;
        if (index < text.Length)
        {
            emit(text[index]);
            index++;
        }
        return true;
    }

    // Reads a quoted string starting at the opening quote at index.
    // Emits the opening quote and the contents, preserving escapes,
    // until the matching closing quote or end of input, advancing index.
    public static void ReadQuotedString(string text, ref int index, char quote, Action<char> emit)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(emit);

        if (index >= text.Length || text[index] != quote)
        {
            return;
        }

        emit(quote);
        index++;

        while (index < text.Length)
        {
            char c = text[index];
            emit(c);
            index++;
            if (c == '\\')
            {
                if (index < text.Length)
                {
                    emit(text[index]);
                    index++;
                }
                continue;
            }
            if (c == quote)
            {
                break;
            }
        }
    }
}
