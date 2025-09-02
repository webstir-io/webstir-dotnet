using System.Text;

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
            Token? t = Next();
            if (t != null && t.Type != TokenType.Whitespace)
            {
                tokens.Add(t);
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
        int sl = _line;
        int sc = _col;
        char c = Advance();
        return c switch
        {
            ' ' or '\t' or '\r' => Make(TokenType.Whitespace, c.ToString(), sl, sc),
            '\n' => Newline(sl, sc),
            '{' => Make(TokenType.OpenBrace, "{", sl, sc),
            '}' => Make(TokenType.CloseBrace, "}", sl, sc),
            '(' => Make(TokenType.OpenParen, "(", sl, sc),
            ')' => Make(TokenType.CloseParen, ")", sl, sc),
            '[' => Make(TokenType.OpenBracket, "[", sl, sc),
            ']' => Make(TokenType.CloseBracket, "]", sl, sc),
            ';' => Make(TokenType.Semicolon, ";", sl, sc),
            ',' => Make(TokenType.Comma, ",", sl, sc),
            '.' => Make(TokenType.Dot, ".", sl, sc),
            '*' => Make(TokenType.Star, "*", sl, sc),
            '=' => Make(TokenType.Equals, "=", sl, sc),
            '"' or '\'' or '`' => ReadString(c, sl, sc),
            '/' when Peek() == '/' => ReadSingleLineComment(sl, sc),
            '/' when Peek() == '*' => ReadMultiLineComment(sl, sc),
            '@' when IsLetter(Peek()) => ReadAtRule(sl, sc),
            _ when IsLetter(c) => ReadIdentifierOrKeyword(c, sl, sc),
            _ when IsDigit(c) => ReadNumber(c, sl, sc),
            _ => Make(TokenType.Unknown, c.ToString(), sl, sc)
        };
    }

    private Token ReadString(char quote, int line, int col)
    {
        StringBuilder sb = new();
        sb.Append(quote);
        while (!IsAtEnd())
        {
            char p = Peek();
            if (p == quote)
            {
                sb.Append(Advance());
                break;
            }
            if (p == '\\')
            {
                sb.Append(Advance());
                if (!IsAtEnd())
                {
                    sb.Append(Advance());
                }
            }
            else
            {
                sb.Append(Advance());
            }
        }
        if (IsAtEnd() && sb.Length > 0 && sb[^1] != quote)
        {
            _diagnostics.AddError("Unterminated string literal", _filePath, line, col);
        }
        return Make(TokenType.String, sb.ToString(), line, col);
    }

    private Token ReadSingleLineComment(int line, int col)
    {
        StringBuilder sb = new();
        sb.Append('/');
        sb.Append(Advance());
        while (!IsAtEnd() && Peek() != '\n')
        {
            sb.Append(Advance());
        }
        return Make(TokenType.SingleLineComment, sb.ToString(), line, col);
    }

    private Token ReadMultiLineComment(int line, int col)
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
            char c = Advance();
            sb.Append(c);
            if (c == '\n')
            {
                _line++;
                _col = 1;
            }
        }
        if (!closed)
        {
            _diagnostics.AddError("Unterminated block comment", _filePath, line, col);
        }
        return Make(TokenType.MultiLineComment, sb.ToString(), line, col);
    }

    private Token ReadAtRule(int line, int col)
    {
        StringBuilder sb = new();
        sb.Append('@');
        while (!IsAtEnd() && IsLetter(Peek()))
        {
            sb.Append(Advance());
        }
        string rule = sb.ToString();
        return Make(rule == "@import" ? TokenType.AtImport : TokenType.Identifier, rule, line, col);
    }

    private Token ReadIdentifierOrKeyword(char first, int line, int col)
    {
        StringBuilder sb = new();
        sb.Append(first);
        while (!IsAtEnd() && (IsLetterOrDigit(Peek()) || Peek() == '_'))
        {
            sb.Append(Advance());
        }
        string id = sb.ToString();
        TokenType type = id switch
        {
            "import" => TokenType.Import,
            "export" => TokenType.Export,
            "from" => TokenType.From,
            "default" => TokenType.Default,
            "as" => TokenType.As,
            _ => TokenType.Identifier
        };
        return Make(type, id, line, col);
    }

    private Token ReadNumber(char first, int line, int col)
    {
        StringBuilder sb = new();
        sb.Append(first);
        while (!IsAtEnd() && (IsDigit(Peek()) || Peek() == '.'))
        {
            sb.Append(Advance());
        }
        return Make(TokenType.Number, sb.ToString(), line, col);
    }

    private Token Newline(int line, int col)
    {
        _line++;
        _col = 1;
        return Make(TokenType.Newline, "\n", line, col);
    }

    private Token Make(TokenType type, string value, int line, int col) => new() { Type = type, Value = value, Line = line, Column = col };
    private char Advance()
    {
        char c = _input[_pos++];
        _col++;
        return c;
    }
    private char Peek() => _pos < _input.Length ? _input[_pos] : '\0';
    private char PeekNext() => _pos + 1 < _input.Length ? _input[_pos + 1] : '\0';
    private bool IsAtEnd() => _pos >= _input.Length;
    private static bool IsLetter(char c) => char.IsLetter(c);
    private static bool IsDigit(char c) => c is >= '0' and <= '9';
    private static bool IsLetterOrDigit(char c) => IsLetter(c) || IsDigit(c);
}
