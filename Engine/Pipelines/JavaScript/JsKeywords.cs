namespace Engine.Pipelines.JavaScript;

internal static class JsKeywords
{
    // Keywords that can precede a regex literal (disambiguating '/' as regex vs division)
    public static readonly string[] RegexPrefixKeywords =
    [
        "return",
        "throw",
        "case",
        "delete",
        "typeof",
        "void",
        "new",
        "in",
        "instanceof",
        "of",
        "yield"
    ];
}

