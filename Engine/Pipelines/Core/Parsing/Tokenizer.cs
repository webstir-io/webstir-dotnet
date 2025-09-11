using System.Collections.Generic;
using System.Text;
using Engine.Extensions;

namespace Engine.Pipelines.Core.Parsing;

public class Tokenizer(string input, string filePath, DiagnosticCollection? diagnostics = null)
{
    private readonly string _input = input;
    private readonly string _filePath = filePath;
    private readonly DiagnosticCollection _diagnostics = diagnostics ?? new DiagnosticCollection();
    private int _pos;
    private int _line = 1;
    private int _col = 1;


    public List<Token> Tokenize()
    {
        List<Token> tokens = [];
        while (!IsAtEnd())
        {
            Token? token = Next();
            if (token != null && token.Type != TokenType.Whitespace)
            {
                tokens.Add(token);
            }
        }
        tokens.Add(new Token { Type = TokenType.EndOfFile, Value = string.Empty, Line = _line, Column = _col });
        return tokens;
    }

    private Token? Next()
    {
        if (IsAtEnd())
        {
            return null;
        }
        int startLine = _line;
        int startColumn = _col;
        char currentChar = Advance();
        return currentChar switch
        {
            ' ' or '\t' or '\r' => CreateToken(TokenType.Whitespace, currentChar.ToString(), startLine, startColumn),
            '\n' => Newline(startLine, startColumn),
            '{' => CreateToken(TokenType.OpenBrace, "{", startLine, startColumn),
            '}' => CreateToken(TokenType.CloseBrace, "}", startLine, startColumn),
            '(' => CreateToken(TokenType.OpenParen, "(", startLine, startColumn),
            ')' => CreateToken(TokenType.CloseParen, ")", startLine, startColumn),
            '[' => CreateToken(TokenType.OpenBracket, "[", startLine, startColumn),
            ']' => CreateToken(TokenType.CloseBracket, "]", startLine, startColumn),
            ';' => CreateToken(TokenType.Semicolon, ";", startLine, startColumn),
            ',' => CreateToken(TokenType.Comma, ",", startLine, startColumn),
            '.' => CreateToken(TokenType.Dot, ".", startLine, startColumn),
            '*' => CreateToken(TokenType.Star, "*", startLine, startColumn),
            '=' => CreateToken(TokenType.Equals, "=", startLine, startColumn),
            '"' or '\'' or '`' => ReadString(currentChar, startLine, startColumn),
            '/' when Peek() == '/' => ReadSingleLineComment(startLine, startColumn),
            '/' when Peek() == '*' => ReadMultiLineComment(startLine, startColumn),
            '@' when IsLetter(Peek()) => ReadAtRule(startLine, startColumn),
            _ when IsLetter(currentChar) => ReadIdentifierOrKeyword(currentChar, startLine, startColumn),
            _ when IsDigit(currentChar) => ReadNumber(currentChar, startLine, startColumn),
            _ => CreateToken(TokenType.Unknown, currentChar.ToString(), startLine, startColumn)
        };
    }

    private Token ReadString(char quote, int line, int column)
    {
        StringBuilder sb = new();
        // We have already consumed the opening quote via Advance() in Next().
        // Start the scanner one character back so it emits the opening quote.
        int oldPos = _pos;
        int index = _pos - 1;
        TextScanner.ReadQuotedString(_input, ref index, quote, character => sb.Append(character));

        // Advance _pos and _col by the number of characters consumed excluding the opening quote
        int consumedExcludingOpening = index - oldPos;
        _pos = index;
        _col += consumedExcludingOpening;

        if (_pos >= _input.Length && sb.Length > 0 && sb[^1] != quote)
        {
            _diagnostics.AddError("Unterminated string literal", _filePath, line, column);
        }

        return CreateToken(TokenType.String, sb.ToString(), line, column);
    }

    private Token ReadSingleLineComment(int line, int column)
    {
        StringBuilder sb = new();
        sb.Append('/');
        sb.Append(Advance());
        while (!IsAtEnd() && Peek() != '\n')
        {
            sb.Append(Advance());
        }
        return CreateToken(TokenType.SingleLineComment, sb.ToString(), line, column);
    }

    private Token ReadMultiLineComment(int line, int column)
    {
        StringBuilder sb = new();
        sb.Append('/');
        sb.Append(Advance());
        bool closed = false;
        while (!IsAtEnd())
        {
            if (Peek() == '*' && PeekNext() == '/')
            {
                sb.Append(Advance());
                sb.Append(Advance());
                closed = true;
                break;
            }
            char character = Advance();
            sb.Append(character);
            if (character == '\n')
            {
                _line++;
                _col = 1;
            }
        }
        if (!closed)
        {
            _diagnostics.AddError("Unterminated block comment", _filePath, line, column);
        }
        return CreateToken(TokenType.MultiLineComment, sb.ToString(), line, column);
    }

    private Token ReadAtRule(int line, int column)
    {
        StringBuilder sb = new();
        sb.Append('@');
        while (!IsAtEnd() && IsLetter(Peek()))
        {
            sb.Append(Advance());
        }
        string rule = sb.ToString();
        return CreateToken(rule == "@import" ? TokenType.AtImport : TokenType.Identifier, rule, line, column);
    }

    private Token ReadIdentifierOrKeyword(char first, int line, int column)
    {
        StringBuilder sb = new();
        sb.Append(first);
        while (!IsAtEnd() && (IsLetterOrDigit(Peek()) || Peek() == '_'))
        {
            sb.Append(Advance());
        }
        string identifier = sb.ToString();
        TokenType type = identifier switch
        {
            "import" => TokenType.Import,
            "export" => TokenType.Export,
            "from" => TokenType.From,
            "default" => TokenType.Default,
            "as" => TokenType.As,
            _ => TokenType.Identifier
        };
        return CreateToken(type, identifier, line, column);
    }

    private Token ReadNumber(char first, int line, int column)
    {
        StringBuilder sb = new();
        sb.Append(first);
        while (!IsAtEnd() && (IsDigit(Peek()) || Peek() == '.'))
        {
            sb.Append(Advance());
        }
        return CreateToken(TokenType.Number, sb.ToString(), line, column);
    }

    private Token Newline(int line, int column)
    {
        _line++;
        _col = 1;
        return CreateToken(TokenType.Newline, "\n", line, column);
    }

    private static Token CreateToken(TokenType type, string value, int line, int column) => new()
    {
        Type = type,
        Value = value,
        Line = line,
        Column = column
    };

    private char Advance()
    {
        char character = _input[_pos++];
        _col++;
        return character;
    }
    private char Peek() => _pos < _input.Length ? _input[_pos] : '\0';
    private char PeekNext() => _pos + 1 < _input.Length ? _input[_pos + 1] : '\0';
    private bool IsAtEnd() => _pos >= _input.Length;
    private static bool IsLetter(char character) => char.IsLetter(character);
    private static bool IsDigit(char character) => character is >= '0' and <= '9';
    private static bool IsLetterOrDigit(char character) => IsLetter(character) || IsDigit(character);
}
