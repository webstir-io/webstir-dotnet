using Engine.Extensions;
using static Engine.Pipelines.JavaScript.Common.Syntax;

namespace Engine.Pipelines.JavaScript.Minification;

internal static class CharacterClassifier
{
    public static bool IsWhitespace(char character) => character.IsAsciiWhitespace();

    public static bool IsLineTerminator(char character) => character.IsLineTerminator();

    public static bool IsIdentifierPart(char character) =>
        char.IsLetterOrDigit(character) || character == '_' || character == '$';

    public static bool ShouldInsertSpace(char previousChar, char nextChar)
    {
        if (previousChar == '\0')
        {
            return false;
        }

        bool prevIsIdent = IsIdentifierPart(previousChar);
        bool nextIsIdent = IsIdentifierPart(nextChar);

        // Space needed between adjacent identifiers
        if (prevIsIdent && nextIsIdent)
        {
            return true;
        }

        // Space needed to prevent ++ or -- operators
        if ((previousChar == PlusChar && nextChar == PlusChar) ||
            (previousChar == MinusChar && nextChar == MinusChar))
        {
            return true;
        }

        // Space needed between digit and identifier
        if ((char.IsDigit(previousChar) && nextIsIdent) ||
            (prevIsIdent && char.IsDigit(nextChar)))
        {
            return true;
        }

        return false;
    }
}
