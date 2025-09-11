using System;
using System.Collections.Generic;
using System.Text;

namespace Engine.Pipelines.Css.Tokenization;

public static class CssSerializer
{
    public static string Serialize(IReadOnlyList<CssToken> tokens)
    {
        if (tokens == null || tokens.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        bool pendingSpace = false;
        char lastNonWhitespaceChar = '\0';
        bool inMediaOrSupportsPrelude = false;

        for (int tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++)
        {
            CssToken token = tokens[tokenIndex];
            if (token.Type == CssTokenType.Eof)
            {
                break;
            }

            // Collapse any whitespace runs to a single pending space
            if (token.Type == CssTokenType.Whitespace)
            {
                pendingSpace = true;
                continue;
            }

            // Drop a semicolon if it's immediately followed by a '}' (ignoring trivia)
            if (token.Type == CssTokenType.Semicolon && IsTrailingSemicolon(tokens, tokenIndex))
            {
                continue;
            }

            // Track @media/@supports prelude
            if (token.Type == CssTokenType.AtKeyword)
            {
                string at = token.Value;
                if (at.Equals("@media", StringComparison.OrdinalIgnoreCase)
                    || at.Equals("@supports", StringComparison.OrdinalIgnoreCase))
                {
                    inMediaOrSupportsPrelude = true;
                }
            }

            if (token.Type == CssTokenType.LBrace)
            {
                inMediaOrSupportsPrelude = false;
            }

            // Enforce spaces around logical operators in media/supports prelude
            if (inMediaOrSupportsPrelude && token.Type == CssTokenType.Ident)
            {
                string ident = token.Value;
                if (ident.Equals("and", StringComparison.OrdinalIgnoreCase)
                    || ident.Equals("or", StringComparison.OrdinalIgnoreCase)
                    || ident.Equals("not", StringComparison.OrdinalIgnoreCase))
                {
                    // Ensure one space before operator
                    if (pendingSpace || lastNonWhitespaceChar != ' ')
                    {
                        builder.Append(' ');
                        lastNonWhitespaceChar = ' ';
                        pendingSpace = false;
                    }

                    builder.Append(token.Value);
                    lastNonWhitespaceChar = LastCharOf(token.Value, lastNonWhitespaceChar);

                    // Ensure one space after operator
                    builder.Append(' ');
                    lastNonWhitespaceChar = ' ';
                    pendingSpace = false;
                    continue;
                }
            }

            FlushPendingSpaceIfNeeded(builder, ref pendingSpace, ref lastNonWhitespaceChar, token);

            // Emit token value as-is (strings, url(), and license comments preserved)
            builder.Append(token.Value);
            lastNonWhitespaceChar = LastCharOf(token.Value, lastNonWhitespaceChar);

            // After a comment, allow a boundary space before the next token if needed
            if (token.Type == CssTokenType.Comment)
            {
                pendingSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static bool IsTrailingSemicolon(IReadOnlyList<CssToken> tokens, int semicolonIndex)
    {
        int nextIndex = semicolonIndex + 1;
        while (nextIndex < tokens.Count && IsTrivia(tokens[nextIndex].Type))
        {
            nextIndex++;
        }
        return nextIndex < tokens.Count && tokens[nextIndex].Type == CssTokenType.RBrace;
    }

    private static bool IsTrivia(CssTokenType type)
        => type is CssTokenType.Whitespace or CssTokenType.Comment;

    private static void FlushPendingSpaceIfNeeded(StringBuilder builder, ref bool pendingSpace, ref char lastNonWhitespaceChar, CssToken nextToken)
    {
        if (!pendingSpace)
        {
            return;
        }

        // Preserve a single space after commas when input had whitespace there.
        // This keeps function argument readability (e.g., linear-gradient(white, black)).
        if (lastNonWhitespaceChar == ',')
        {
            builder.Append(' ');
            lastNonWhitespaceChar = ' ';
            pendingSpace = false;
            return;
        }

        // Avoid space before closers or immediately after openers
        if (!IsCloser(nextToken.Type) && !IsPrevOpener(lastNonWhitespaceChar))
        {
            if (RequiresBoundarySpace(lastNonWhitespaceChar, FirstChar(nextToken)))
            {
                builder.Append(' ');
                lastNonWhitespaceChar = ' ';
            }
        }
        pendingSpace = false;
    }

    private static bool IsCloser(CssTokenType type)
        => type is CssTokenType.RBrace or CssTokenType.RParen or CssTokenType.RBracket or CssTokenType.Comma or CssTokenType.Semicolon or CssTokenType.Colon;

    private static bool IsPrevOpener(char character)
        => character is '(' or '[' or '{';

    private static char FirstChar(CssToken token) => token.Value.Length > 0 ? token.Value[0] : '\0';

    private static char LastCharOf(string text, char fallback)
    {
        for (int reverseIndex = text.Length - 1; reverseIndex >= 0; reverseIndex--)
        {
            char ch = text[reverseIndex];
            if (!char.IsWhiteSpace(ch))
            {
                return ch;
            }
        }
        return fallback;
    }

    private static bool IsAlphaNum(char character)
        => char.IsLetterOrDigit(character) || character == '_' || character == '-';

    private static bool RequiresBoundarySpace(char prev, char next)
    {
        if (prev == '\0' || next == '\0')
        {
            return false;
        }

        if (IsPrevOpener(prev))
        {
            return false;
        }

        // No space needed before common closers or punctuation
        if (next is ')' or ']' or '}' or ':' or ';' or ',')
        {
            return false;
        }

        // Space when two identifier-like pieces could merge
        if (IsAlphaNum(prev) && IsAlphaNum(next))
        {
            return true;
        }

        // Prevent accidental number/dimension merge like "0" + ".5em" => "0.5em"
        if (char.IsDigit(prev) && next == '.')
        {
            return true;
        }

        return false;
    }
}
