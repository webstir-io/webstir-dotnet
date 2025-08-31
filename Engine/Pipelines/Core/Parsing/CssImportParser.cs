namespace Engine.Pipelines.Core.Parsing;

public class CssImportParser
{
    private readonly List<Token> _tokens;
    private readonly DiagnosticCollection _diagnostics;
    private int _current;

    public CssImportParser(string code, string filePath, DiagnosticCollection? diagnostics = null)
    {
        _diagnostics = diagnostics ?? new DiagnosticCollection();
        _tokens = new Tokenizer(code, filePath, _diagnostics).Tokenize();
    }

    public List<CssImportRule> ParseImports()
    {
        List<CssImportRule> list = [];
        _current = 0;
        while (!IsAtEnd())
        {
            if (Match(TokenType.AtImport))
            {
                var stmt = ParseImport();
                if (stmt != null) list.Add(stmt);
            }
            else Advance();
        }
        return list;
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
            if (Match(TokenType.OpenParen) && Check(TokenType.String))
            {
                path = StripQuotes(Advance().Value);
                Match(TokenType.CloseParen);
            }
        }
        if (path == null) return null;

        // collect media tail until semicolon or EOF
        List<string> parts = [];
        while (!Check(TokenType.Semicolon) && !IsAtEnd())
        {
            parts.Add(Current().Value);
            Advance();
        }
        Match(TokenType.Semicolon);

        string? media = parts.Count > 0 ? string.Join(" ", parts).Trim() : null;
        return new CssImportRule { Path = path, Media = string.IsNullOrWhiteSpace(media) ? null : media };
    }

    private void SkipTrivia()
    {
        while (Match(TokenType.Whitespace, TokenType.Newline, TokenType.SingleLineComment, TokenType.MultiLineComment)) { }
    }

    private static string StripQuotes(string s)
    {
        if (s.Length >= 2 && (s[0] == '"' || s[0] == '\'' || s[0] == '`')) return s[1..^1];
        return s;
    }

    private bool Match(params TokenType[] types)
    {
        foreach (var t in types) if (Check(t)) { Advance(); return true; }
        return false;
    }
    private bool Check(TokenType t) => !IsAtEnd() && Current().Type == t;
    private Token Advance() => _tokens[_current++];
    private Token Current() => _tokens[_current];
    private bool IsAtEnd() => Current().Type == TokenType.EndOfFile;
}

public class CssImportRule
{
    public required string Path { get; set; }
    public string? Media { get; set; }
}
