using System;
using System.Collections.Generic;
using Engine.Extensions;

namespace Engine.Pipelines.Css.Tokenization;

public sealed class CssTokenizer(string css)
{
    private readonly string input = css ?? string.Empty;
    private int index = 0;

    public List<CssToken> Tokenize(bool preserveLicenseComments = true)
    {
        List<CssToken> tokens = [];

        while (!IsEof())
        {
            int segmentStart = index;
            char currentChar = Peek();

            if (char.IsWhiteSpace(currentChar))
            {
                tokens.Add(ReadWhitespace());
                continue;
            }

            if (currentChar == '/' && Peek(1) == '*')
            {
                CssToken? comment = ReadComment(preserveLicenseComments);
                if (comment.HasValue)
                {
                    tokens.Add(comment.Value);
                }
                continue;
            }

            if (currentChar is '\'' or '"')
            {
                tokens.Add(ReadString());
                continue;
            }

            if (IsUrlStart())
            {
                tokens.Add(ReadUrl());
                continue;
            }

            if (currentChar == '@')
            {
                tokens.Add(ReadAtKeyword());
                continue;
            }

            if (char.IsDigit(currentChar) || (currentChar == '.' && char.IsDigit(Peek(1))))
            {
                tokens.Add(ReadNumberOrDimensionOrPercentage());
                continue;
            }

            if (currentChar == '#')
            {
                tokens.Add(ReadHash());
                continue;
            }

            if (TryReadPunctuation(segmentStart, out CssToken punct))
            {
                tokens.Add(punct);
                continue;
            }

            if (IsNameStart(currentChar))
            {
                tokens.Add(ReadIdent());
                continue;
            }

            // fallback: single-character delimiter
            tokens.Add(Make(CssTokenType.Delim, Consume().ToString(), segmentStart));
        }

        tokens.Add(new CssToken(CssTokenType.Eof, string.Empty, index, index));
        return tokens;
    }

    private bool TryReadPunctuation(int segmentStart, out CssToken token)
    {
        token = default;
        char ch = Peek();
        CssTokenType type;
        switch (ch)
        {
            case '{':
                type = CssTokenType.LBrace;
                break;
            case '}':
                type = CssTokenType.RBrace;
                break;
            case '(':
                type = CssTokenType.LParen;
                break;
            case ')':
                type = CssTokenType.RParen;
                break;
            case '[':
                type = CssTokenType.LBracket;
                break;
            case ']':
                type = CssTokenType.RBracket;
                break;
            case ':':
                type = CssTokenType.Colon;
                break;
            case ';':
                type = CssTokenType.Semicolon;
                break;
            case ',':
                type = CssTokenType.Comma;
                break;
            default:
                return false;
        }

        token = Make(type, Consume().ToString(), segmentStart);
        return true;
    }

    private CssToken ReadWhitespace()
    {
        int segmentStart = index;
        while (!IsEof() && char.IsWhiteSpace(Peek()))
        {
            Consume();
        }
        return new CssToken(CssTokenType.Whitespace, input[segmentStart..index], segmentStart, index);
    }

    private CssToken? ReadComment(bool preserveLicense)
    {
        int segmentStart = index;
        Consume(); // '/'
        Consume(); // '*'
        bool important = Peek() == '!';
        if (important)
        {
            Consume();
        }

        while (!IsEof())
        {
            if (Peek() == '*' && Peek(1) == '/')
            {
                Consume();
                Consume();
                break;
            }
            Consume();
        }

        if (important && preserveLicense)
        {
            return new CssToken(CssTokenType.Comment, input[segmentStart..index], segmentStart, index);
        }

        return null;
    }

    private CssToken ReadString()
    {
        int segmentStart = index;
        char quote = Consume();
        int local = index - 1; // position of opening quote
        TextScanner.ReadQuotedString(input, ref local, quote, _ => { });
        index = local;
        return new CssToken(CssTokenType.String, input[segmentStart..index], segmentStart, index);
    }

    private bool IsUrlStart()
    {
        if (index + 3 > input.Length)
        {
            return false;
        }
        string word = input[index..Math.Min(index + 3, input.Length)];
        if (!word.Equals("url", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int afterNameIndex = index + 3;
        while (afterNameIndex < input.Length && char.IsWhiteSpace(input[afterNameIndex]))
        {
            afterNameIndex++;
        }
        return afterNameIndex < input.Length && input[afterNameIndex] == '(';
    }

    private CssToken ReadUrl()
    {
        int segmentStart = index;
        // read 'url'
        Consume();
        Consume();
        Consume();
        // optional whitespace
        while (!IsEof() && char.IsWhiteSpace(Peek()))
        {
            Consume();
        }
        if (!IsEof() && Peek() == '(')
        {
            Consume();
        }

        // read until the matching ')', respecting quotes and escapes
        bool inString = false;
        char strQuote = '\0';
        while (!IsEof())
        {
            char currentChar = Consume();
            if (inString)
            {
                if (currentChar == '\\')
                {
                    if (!IsEof())
                    {
                        Consume();
                    }
                    continue;
                }
                if (currentChar == strQuote)
                {
                    inString = false;
                    strQuote = '\0';
                    continue;
                }
                continue;
            }
            if (currentChar is '\'' or '"')
            {
                inString = true;
                strQuote = currentChar;
                continue;
            }
            if (currentChar == ')')
            {
                break;
            }
        }

        return new CssToken(CssTokenType.Url, input[segmentStart..index], segmentStart, index);
    }

    private CssToken ReadAtKeyword()
    {
        int segmentStart = index;
        Consume(); // '@'
        while (!IsEof() && IsNameChar(Peek()))
        {
            Consume();
        }
        return new CssToken(CssTokenType.AtKeyword, input[segmentStart..index], segmentStart, index);
    }

    private CssToken ReadIdent()
    {
        int segmentStart = index;
        Consume();
        while (!IsEof() && IsNameChar(Peek()))
        {
            Consume();
        }
        return new CssToken(CssTokenType.Ident, input[segmentStart..index], segmentStart, index);
    }

    private CssToken ReadNumberOrDimensionOrPercentage()
    {
        int segmentStart = index;
        ReadNumber();
        if (!IsEof())
        {
            if (Peek() == '%')
            {
                Consume();
                return new CssToken(CssTokenType.Percentage, input[segmentStart..index], segmentStart, index);
            }
            if (IsNameStart(Peek()))
            {
                while (!IsEof() && IsNameChar(Peek()))
                {
                    Consume();
                }
                return new CssToken(CssTokenType.Dimension, input[segmentStart..index], segmentStart, index);
            }
        }
        return new CssToken(CssTokenType.Number, input[segmentStart..index], segmentStart, index);
    }

    private void ReadNumber()
    {
        char sign = Peek();
        if (sign is '+' or '-')
        {
            Consume();
        }
        while (!IsEof() && char.IsDigit(Peek()))
        {
            Consume();
        }
        if (!IsEof() && Peek() == '.')
        {
            if (char.IsDigit(Peek(1)))
            {
                Consume();
                while (!IsEof() && char.IsDigit(Peek()))
                {
                    Consume();
                }
            }
        }
        if (!IsEof() && Peek() is 'e' or 'E')
        {
            int save = index;
            Consume();
            char expSign = Peek();
            if (expSign is '+' or '-')
            {
                Consume();
            }
            if (char.IsDigit(Peek()))
            {
                while (!IsEof() && char.IsDigit(Peek()))
                {
                    Consume();
                }
            }
            else
            {
                index = save;
            }
        }
    }

    private CssToken ReadHash()
    {
        int segmentStart = index;
        Consume(); // '#'
        while (!IsEof() && IsHex(Peek()))
        {
            Consume();
        }
        return new CssToken(CssTokenType.Hash, input[segmentStart..index], segmentStart, index);
    }

    private bool IsNameStart(char character) => char.IsLetter(character) || character == '_' || character == '-';

    private bool IsNameChar(char character) => char.IsLetterOrDigit(character) || character == '_' || character == '-';

    private bool IsHex(char character) => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private char Peek(int lookahead = 0)
    {
        int absoluteIndex = index + lookahead;
        if (absoluteIndex >= input.Length)
        {
            return '\0';
        }
        return input[absoluteIndex];
    }

    private char Consume()
    {
        char currentChar = input[index];
        index++;
        return currentChar;
    }

    private bool IsEof() => index >= input.Length;

    private static CssToken Make(CssTokenType type, string value, int start) => new(type, value, start, start + value.Length);
}
