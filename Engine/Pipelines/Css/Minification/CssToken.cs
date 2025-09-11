using System;

namespace Engine.Pipelines.Css.Minification;

public enum CssTokenType
{
    Whitespace,
    Ident,
    AtKeyword,
    String,
    Url,
    Number,
    Dimension,
    Percentage,
    Hash,
    Colon,
    Semicolon,
    Comma,
    Delim,
    LBrace,
    RBrace,
    LParen,
    RParen,
    LBracket,
    RBracket,
    Comment,
    Eof
}

public readonly record struct CssToken(CssTokenType Type, string Value, int Start, int End)
{
    public override string ToString() => FormattableString.Invariant($"{Type}('{Value}') @{Start}-{End}");
}
