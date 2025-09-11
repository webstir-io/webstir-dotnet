using System.Collections.Generic;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Parsing;
using Engine.Pipelines.Core.Utilities;

namespace Engine.Pipelines.Css.Parsing;

public sealed class CssImportParser
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
        _current = 0;
    }

    public List<CssImportRule> ParseImports()
    {
        List<CssImportRule> imports = [];
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
        SkipTrivia();

        string? path = ExtractImportPath();
        if (path == null)
        {
            return null;
        }

        string? media = ExtractMediaQuery();
        ValidateSemicolon();

        return new CssImportRule
        {
            Path = path,
            Media = string.IsNullOrWhiteSpace(media) ? null : media
        };
    }

    private string? ExtractImportPath()
    {
        if (Check(TokenType.String))
        {
            return StripQuotes(Advance().Value);
        }

        if (Check(TokenType.Identifier) && Current().Value == "url")
        {
            return ExtractUrlPath();
        }

        return null;
    }

    private string? ExtractUrlPath()
    {
        Advance(); // consume 'url'

        if (!Match(TokenType.OpenParen))
        {
            return null;
        }

        string? path = Check(TokenType.String)
            ? StripQuotes(Advance().Value)
            : ExtractUnquotedUrl();

        Match(TokenType.CloseParen);
        return path;
    }

    private string ExtractUnquotedUrl()
    {
        List<string> tokenBuffer = [];

        while (!Check(TokenType.CloseParen) && !IsAtEnd())
        {
            Token token = Current();
            if (!IsTrivia(token.Type))
            {
                tokenBuffer.Add(token.Value);
            }
            Advance();
        }

        return StripQuotes(string.Join("", tokenBuffer).Trim());
    }

    private string? ExtractMediaQuery()
    {
        List<string> mediaParts = [];

        while (!Check(TokenType.Semicolon) && !IsAtEnd())
        {
            mediaParts.Add(Current().Value);
            Advance();
        }

        return mediaParts.Count > 0
            ? string.Join(" ", mediaParts).Trim()
            : null;
    }

    private void ValidateSemicolon()
    {
        if (!Match(TokenType.Semicolon))
        {
            _diagnostics.AddError("Expected ';' after @import rule", _filePath, Current().Line, Current().Column);
        }
    }

    private static bool IsTrivia(TokenType type) =>
        type is TokenType.Whitespace or TokenType.Newline or TokenType.SingleLineComment or TokenType.MultiLineComment;

    private void SkipTrivia()
    {
        while (Match(TokenType.Whitespace, TokenType.Newline, TokenType.SingleLineComment, TokenType.MultiLineComment))
        {
        }
    }

    private static string StripQuotes(string text)
    {
        if (text.Length >= 2 && text[0] is '"' or '\'' or '`')
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

public sealed class CssImportRule
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
