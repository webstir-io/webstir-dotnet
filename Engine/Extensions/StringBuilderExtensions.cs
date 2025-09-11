using System;
using System.Text;

namespace Engine.Extensions;

public static class StringBuilderExtensions
{
    public static bool EndsWithToken(this StringBuilder builder, string token, Func<char, bool> isIdentifierPart)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(isIdentifierPart);

        int length = builder.Length;
        if (length < token.Length)
        {
            return false;
        }

        int startIndex = length - token.Length;
        for (int tokenIndex = 0; tokenIndex < token.Length; tokenIndex++)
        {
            if (builder[startIndex + tokenIndex] != token[tokenIndex])
            {
                return false;
            }
        }

        char before = startIndex > 0 ? builder[startIndex - 1] : '\0';
        char after = length > startIndex + token.Length ? builder[startIndex + token.Length] : '\0';
        bool beforeOk = before == '\0' || !isIdentifierPart(before);
        bool afterOk = after == '\0' || !isIdentifierPart(after);
        return beforeOk && afterOk;
    }

    public static bool EndsWithAnyToken(this StringBuilder builder, Func<char, bool> isIdentifierPart, params string[] tokens)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(isIdentifierPart);
        ArgumentNullException.ThrowIfNull(tokens);

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (builder.EndsWithToken(token, isIdentifierPart))
            {
                return true;
            }
        }
        return false;
    }
}
