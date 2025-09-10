using System;
using System.Collections.Generic;
using System.Globalization;

namespace Engine.Pipelines.Css.Tokenization;

public static class CssTokenMinifier
{
    public static List<CssToken> Minify(IReadOnlyList<CssToken> tokens)
    {
        List<CssToken> output = [];
        if (tokens == null || tokens.Count == 0)
        {
            return output;
        }

        for (int tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++)
        {
            CssToken token = tokens[tokenIndex];
            if (token.Type == CssTokenType.Eof)
            {
                break;
            }

            switch (token.Type)
            {
                case CssTokenType.Whitespace:
                    // Keep a single whitespace token; serializer will insert space only if needed
                    output.Add(new CssToken(CssTokenType.Whitespace, " ", token.Start, token.End));
                    break;

                case CssTokenType.Number:
                    output.Add(NormalizeNumber(token));
                    break;

                case CssTokenType.Percentage:
                    output.Add(NormalizePercentage(token));
                    break;

                case CssTokenType.Dimension:
                    output.Add(NormalizeDimension(token));
                    break;

                case CssTokenType.Hash:
                    output.Add(ShortenHexColor(token));
                    break;

                default:
                    output.Add(token);
                    break;
            }
        }

        // Ensure EOF token at end
        output.Add(new CssToken(CssTokenType.Eof, string.Empty, 0, 0));
        return output;
    }

    private static CssToken NormalizeNumber(CssToken token)
    {
        string text = token.Value;
        // Fast-path: avoid changing scientific notation or non-standard formats
        if (text.Contains('e') || text.Contains('E'))
        {
            return token;
        }

        string trimmed = text.Trim();
        if (!TryParseDecimal(trimmed, out decimal value))
        {
            return token;
        }

        if (value == 0)
        {
            return new CssToken(CssTokenType.Number, "0", token.Start, token.End);
        }

        string normalized = FormatDecimal(value, removeLeadingZeroForFractions: true);
        return new CssToken(CssTokenType.Number, normalized, token.Start, token.End);
    }

    private static CssToken NormalizePercentage(CssToken token)
    {
        string text = token.Value;
        if (text.Length == 0 || text[^1] != '%')
        {
            return token;
        }
        string num = text[..^1];
        if (num.Contains('e') || num.Contains('E'))
        {
            return token;
        }
        if (!TryParseDecimal(num, out decimal value))
        {
            return token;
        }
        if (value == 0)
        {
            return new CssToken(CssTokenType.Number, "0", token.Start, token.End);
        }
        string normalized = FormatDecimal(value, removeLeadingZeroForFractions: true) + "%";
        return new CssToken(CssTokenType.Percentage, normalized, token.Start, token.End);
    }

    private static CssToken NormalizeDimension(CssToken token)
    {
        string text = token.Value;
        // split numeric part and unit
        int split = IndexOfUnitStart(text);
        if (split <= 0 || split >= text.Length)
        {
            return token;
        }
        string num = text[..split];
        string unit = text[split..];

        if (num.Contains('e') || num.Contains('E'))
        {
            return token;
        }
        if (!TryParseDecimal(num, out decimal value))
        {
            return token;
        }

        if (value == 0 && IsZeroUnitStrippable(unit))
        {
            return new CssToken(CssTokenType.Number, "0", token.Start, token.End);
        }

        string normalized = FormatDecimal(value, removeLeadingZeroForFractions: true) + unit;
        return new CssToken(CssTokenType.Dimension, normalized, token.Start, token.End);
    }

    private static CssToken ShortenHexColor(CssToken token)
    {
        string text = token.Value;
        if (text.Length == 7 && text[0] == '#')
        {
            char r1 = text[1], r2 = text[2];
            char g1 = text[3], g2 = text[4];
            char b1 = text[5], b2 = text[6];

            if (EqualHex(r1, r2) && EqualHex(g1, g2) && EqualHex(b1, b2))
            {
                string shortHex = new(['#', ToLowerHex(r1), ToLowerHex(g1), ToLowerHex(b1)]);
                return new CssToken(CssTokenType.Hash, shortHex, token.Start, token.End);
            }
        }
        return token;
    }

    private static bool TryParseDecimal(string text, out decimal value)
        => decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);

    private static string FormatDecimal(decimal value, bool removeLeadingZeroForFractions)
    {
        // Use InvariantCulture to avoid locale decimals
        string raw = value.ToString(CultureInfo.InvariantCulture);

        // Strip trailing .0+ -> integer
        if (raw.Contains('.'))
        {
            // Trim trailing zeros
            int lastIndex = raw.Length - 1;
            while (lastIndex >= 0 && raw[lastIndex] == '0')
            {
                lastIndex--;
            }
            if (lastIndex >= 0 && raw[lastIndex] == '.')
            {
                lastIndex--;
            }
            raw = raw[..(lastIndex + 1)];
        }

        // Remove leading zero for fractional values between -1 and 1 excluding 0
        if (removeLeadingZeroForFractions)
        {
            if (raw.StartsWith("0.", StringComparison.Ordinal))
            {
                return raw[1..];
            }
            if (raw.StartsWith("-0.", StringComparison.Ordinal))
            {
                return "-" + raw[2..];
            }
        }

        return raw;
    }

    private static int IndexOfUnitStart(string text)
    {
        for (int charIndex = 0; charIndex < text.Length; charIndex++)
        {
            char ch = text[charIndex];
            if (!(char.IsDigit(ch) || ch == '.' || ch == '+' || ch == '-'))
            {
                return charIndex;
            }
        }
        return -1;
    }

    private static bool IsZeroUnitStrippable(string unit)
    {
        // Modern safe set, case-insensitive
        return unit.Equals("px", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("em", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("rem", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("%", StringComparison.Ordinal) // percent is single char, case-sensitive
            || unit.Equals("in", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("cm", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("mm", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("pc", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("pt", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("ex", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("vh", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("vw", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("vmin", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("vmax", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("ch", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EqualHex(char left, char right) => ToLowerHex(left) == ToLowerHex(right);

    private static char ToLowerHex(char character)
    {
        if (character is >= 'A' and <= 'F')
        {
            return (char)(character + 32);
        }
        return character;
    }
}
