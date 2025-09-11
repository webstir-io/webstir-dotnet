namespace Engine.Extensions;

public static class CharExtensions
{
    public static bool IsAsciiWhitespace(this char character) => character is ' ' or '\t' or '\n' or '\r' or '\f' or '\v';

    public static bool IsLineTerminator(this char character) => character is '\n' or '\r';
}
