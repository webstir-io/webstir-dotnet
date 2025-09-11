using System.Text;
using Engine.Extensions;
using Engine.Pipelines.JavaScript.Common;

namespace Engine.Pipelines.JavaScript.Minification;

internal static class RegexDetector
{
    public static bool ShouldStartRegex(char lastNonWhitespaceChar, StringBuilder output)
    {
        return JsRegex.RegexPrefixChars.Contains(lastNonWhitespaceChar) ||
               EndsWithRegexKeyword(output);
    }

    private static bool EndsWithRegexKeyword(StringBuilder builder) =>
        builder.EndsWithAnyToken(CharacterClassifier.IsIdentifierPart, JsKeywords.RegexPrefixKeywords);
}
