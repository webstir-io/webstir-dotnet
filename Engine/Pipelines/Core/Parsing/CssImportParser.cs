namespace Engine.Pipelines.Core.Parsing;

public class CssImportParser
{
    private readonly List<Token> _tokens;
    private readonly DiagnosticCollection _diagnostics;
    private readonly string _filePath;
    private int _current;

    public CssImportParser(string code, string filePath, DiagnosticCollection? diagnostics = null)
    {
        _filePath = filePath;
        _diagnostics = diagnostics ?? new DiagnosticCollection();
        _tokens = new Tokenizer(code, filePath, _diagnostics).Tokenize();
    }

    public List<CssImportRule> ParseImports()
    {
        List<CssImportRule> imports = [];
        _current = 0;
        while (!IsAtEnd())
        {
            if (Match(TokenType.AtImport))
            {
                CssImportRule? importRule = ParseImport();
                if (importRule != null)
                {
                    imports.Add(importRule);
                }
            }
            else
            {
                Advance();
            }
        }
        return imports;
    }

    private CssImportRule? ParseImport()
    {
        // @import "x.css" [media...];
        SkipTrivia();
        string? path = null;
        if (Check(TokenType.String))
        {
            path = StripQuotes(Advance().Value);
        }
        else if (Check(TokenType.Identifier) && Current().Value == "url")
        {
            Advance();
            if (Match(TokenType.OpenParen))
            {
                // Accept url('x.css') or url(x.css)
                if (Check(TokenType.String))
                {
                    path = StripQuotes(Advance().Value);
                }
                else
                {
                    List<string> tokenBuffer = [];
                    while (!Check(TokenType.CloseParen) && !IsAtEnd())
                    {
                        Token token = Current();
                        if (token.Type is not TokenType.Whitespace and not TokenType.Newline and not TokenType.SingleLineComment and not TokenType.MultiLineComment)
                        {
                            tokenBuffer.Add(token.Value);
                        }
                        Advance();
                    }
                    string rawUrl = string.Join("", tokenBuffer).Trim();
                    path = StripQuotes(rawUrl);
                }
                Match(TokenType.CloseParen);
            }
        }
        if (path == null)
        {
            return null;
        }

        // collect media tail until semicolon or EOF
        List<string> mediaParts = [];
        while (!Check(TokenType.Semicolon) && !IsAtEnd())
        {
            mediaParts.Add(Current().Value);
            Advance();
        }
        bool hadSemicolon = Match(TokenType.Semicolon);
        if (!hadSemicolon)
        {
            _diagnostics.AddError("Expected ';' after @import rule", _filePath, Current().Line, Current().Column);
        }

        string? media = mediaParts.Count > 0 ? string.Join(" ", mediaParts).Trim() : null;
        return new CssImportRule { Path = path, Media = string.IsNullOrWhiteSpace(media) ? null : media };
    }

    private void SkipTrivia()
    {
        while (Match(TokenType.Whitespace, TokenType.Newline, TokenType.SingleLineComment, TokenType.MultiLineComment))
        {
        }
    }

    private static string StripQuotes(string text)
    {
        if (text.Length >= 2 && (text[0] == '"' || text[0] == '\'' || text[0] == '`'))
        {
            return text[1..^1];
        }
        return text;
    }

    private bool Match(params TokenType[] types)
    {
        foreach (TokenType tokenType in types)
        {
            if (Check(tokenType))
            {
                Advance();
                return true;
            }
        }

        return false;
    }
    private bool Check(TokenType type) => !IsAtEnd() && Current().Type == type;
    private Token Advance() => _tokens[_current++];
    private Token Current() => _tokens[_current];
    private bool IsAtEnd() => Current().Type == TokenType.EndOfFile;
}

public class CssImportRule
{
    public required string Path
    {
        get; set;
    }
    public string? Media
    {
        get; set;
    }
}
