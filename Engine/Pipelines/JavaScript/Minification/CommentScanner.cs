using System;
using Engine.Extensions;
using static Engine.Pipelines.JavaScript.Common.Syntax;

namespace Engine.Pipelines.JavaScript.Minification;

internal static class CommentScanner
{
    public static void SkipLineComment(string code, MinifyState state)
    {
        int scanIndex = state.Index + 2;
        while (scanIndex < code.Length && !code[scanIndex].IsLineTerminator())
        {
            scanIndex++;
        }
        state.Index = scanIndex;
    }

    public static (bool isLicense, int endExclusive) ScanBlockComment(string code, MinifyState state)
    {
        int scanIndex = state.Index + 2;
        bool isLicense = scanIndex < code.Length && code[scanIndex] == ExclamationChar;

        int blockCommentEnd = scanIndex;
        while (blockCommentEnd + 1 < code.Length &&
               !(code[blockCommentEnd] == AsteriskChar && code[blockCommentEnd + 1] == SlashChar))
        {
            blockCommentEnd++;
        }

        int endExclusive = Math.Min(blockCommentEnd + 2, code.Length);
        return (isLicense, endExclusive);
    }
}
