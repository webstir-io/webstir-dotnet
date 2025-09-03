namespace Engine.Pipelines.Core.Parsing;

public enum TokenType
{
    // Literals
    String,
    Number,
    Identifier,

    // Keywords
    Import,
    Export,
    From,
    Default,
    As,

    // Symbols
    OpenBrace,      // {
    CloseBrace,     // }
    OpenParen,      // (
    CloseParen,     // )
    OpenBracket,    // [
    CloseBracket,   // ]
    Semicolon,      // ;
    Comma,          // ,
    Dot,            // .
    Star,           // *
    Equals,         // =

    // Comments
    SingleLineComment,
    MultiLineComment,

    // CSS specific
    AtImport,       // @import

    // Special
    Whitespace,
    Newline,
    EndOfFile,
    Unknown
}

public class Token
{
    public required TokenType Type
    {
        get; init;
    }
    public required string Value
    {
        get; init;
    }
    public required int Line
    {
        get; init;
    }
    public required int Column
    {
        get; init;
    }
}

