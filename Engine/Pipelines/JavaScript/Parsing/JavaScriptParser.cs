using System.Collections.Generic;
using Engine.Pipelines.Core.Parsing;
using Engine.Pipelines.Core.Utilities;

namespace Engine.Pipelines.JavaScript.Parsing;

public class JavaScriptParser
{
    private readonly List<Token> _tokens;
    private readonly string _filePath;
    private readonly DiagnosticCollection _diagnostics;
    private int _current;

    public JavaScriptParser(string code, string filePath, DiagnosticCollection? diagnostics = null)
    {
        _filePath = filePath;
        _diagnostics = diagnostics ?? new DiagnosticCollection();
        _tokens = new Tokenizer(code, filePath, _diagnostics).Tokenize();
    }

    public List<ImportDeclaration> ParseImports()
    {
        List<ImportDeclaration> list = [];
        _current = 0;
        while (!IsAtEnd())
        {
            if (Match(TokenType.Import))
            {
                ImportDeclaration? imp = ParseImport();
                if (imp != null)
                {
                    list.Add(imp);
                }
            }
            else
            {
                Advance();
            }
        }
        return list;
    }

    public List<ExportDeclaration> ParseExports()
    {
        List<ExportDeclaration> list = [];
        _current = 0;
        while (!IsAtEnd())
        {
            if (Match(TokenType.Export))
            {
                ExportDeclaration? ex = ParseExport();
                if (ex != null)
                {
                    list.Add(ex);
                }
            }
            else
            {
                Advance();
            }
        }
        return list;
    }

    private ImportDeclaration? ParseImport()
    {
        Token tImport = Previous();
        SkipTrivia();

        // dynamic import('x') recognized when next is OpenParen
        if (Check(TokenType.OpenParen))
        {
            Advance();
            if (Check(TokenType.String))
            {
                string src = StripQuotes(Advance().Value);
                // consume until ')'
                while (!IsAtEnd() && !Match(TokenType.CloseParen))
                {
                    Advance();
                }
                return new ImportDeclaration { Source = src, IsDynamic = true, Line = tImport.Line, Column = tImport.Column };
            }
            return null;
        }

        // import type { X } from 'mod' — ignore for runtime graph
        bool typeOnly = false;
        if (Check(TokenType.Identifier) && Current().Value == "type")
        {
            typeOnly = true;
            Advance();
            SkipTrivia();
        }

        // side-effect import: import "x";
        if (Check(TokenType.String))
        {
            string src = StripQuotes(Advance().Value);
            if (typeOnly)
            {
                return null;
            }
            return new ImportDeclaration { Source = src, IsSideEffect = true, Line = tImport.Line, Column = tImport.Column };
        }

        ImportDeclaration imp = new()
        {
            Line = tImport.Line,
            Column = tImport.Column
        };

        if (Match(TokenType.Star))
        {
            if (Match(TokenType.As) && Check(TokenType.Identifier))
            {
                imp.NamespaceImport = Advance().Value;
            }
        }
        else if (Check(TokenType.Identifier))
        {
            // default import
            imp.DefaultImport = Advance().Value;
            if (Match(TokenType.Comma) && Match(TokenType.OpenBrace))
            {
                imp.NamedImports = ParseNamedList();
                Match(TokenType.CloseBrace);
            }
        }
        else if (Match(TokenType.OpenBrace))
        {
            imp.NamedImports = ParseNamedList();
            Match(TokenType.CloseBrace);
        }

        // expect from "..."
        SkipTrivia();
        if (!Match(TokenType.From))
        {
            _diagnostics.AddError("Expected 'from' in import", _filePath, Current().Line, Current().Column);
            return null;
        }
        SkipTrivia();
        if (!Check(TokenType.String))
        {
            _diagnostics.AddError("Expected module string in import", _filePath, Current().Line, Current().Column);
            return null;
        }
        imp.Source = StripQuotes(Advance().Value);
        if (typeOnly)
        {
            return null;
        }
        return imp;
    }

    private List<NamedImport> ParseNamedList()
    {
        List<NamedImport> list = [];
        while (!Check(TokenType.CloseBrace) && !IsAtEnd())
        {
            SkipTrivia();
            if (Check(TokenType.Identifier))
            {
                string imported = Advance().Value;
                string local = imported;
                if (Match(TokenType.As) && Check(TokenType.Identifier))
                {
                    local = Advance().Value;
                }
                list.Add(new NamedImport { Imported = imported, Local = local });
            }
            if (!Match(TokenType.Comma))
            {
                break;
            }
        }
        return list;
    }

    private ExportDeclaration? ParseExport()
    {
        Token tExport = Previous();
        SkipTrivia();

        // export default ...
        if (Match(TokenType.Default))
        {
            return new ExportDeclaration { IsDefault = true, Line = tExport.Line, Column = tExport.Column };
        }

        // export * as NS from "x" OR export * from "x"
        if (Match(TokenType.Star))
        {
            SkipTrivia();
            if (Match(TokenType.As) && Check(TokenType.Identifier))
            {
                string ns = Advance().Value;
                SkipTrivia();
                if (Match(TokenType.From) && Check(TokenType.String))
                {
                    return new ExportDeclaration { Source = StripQuotes(Advance().Value), IsReExport = true, Line = tExport.Line, Column = tExport.Column, Namespace = ns };
                }
                return null;
            }

            if (Match(TokenType.From) && Check(TokenType.String))
            {
                return new ExportDeclaration { Source = StripQuotes(Advance().Value), IsReExport = true, Line = tExport.Line, Column = tExport.Column, All = true };
            }
            return null;
        }

        // export { a, b } [from "x"]
        if (Match(TokenType.OpenBrace))
        {
            List<string> names = [];
            while (!Check(TokenType.CloseBrace) && !IsAtEnd())
            {
                if (Check(TokenType.Identifier))
                {
                    string name = Advance().Value;
                    // optional "as local"; export surface is still the local
                    if (Match(TokenType.As) && Check(TokenType.Identifier))
                    {
                        name = Advance().Value;
                    }
                    names.Add(name);
                }
                if (!Match(TokenType.Comma))
                {
                    break;
                }
            }
            Match(TokenType.CloseBrace);
            SkipTrivia();
            ExportDeclaration stmt = new()
            {
                Named = names,
                Line = tExport.Line,
                Column = tExport.Column
            };
            if (Match(TokenType.From) && Check(TokenType.String))
            {
                stmt.Source = StripQuotes(Advance().Value);
                stmt.IsReExport = true;
            }
            return stmt;
        }

        return null;
    }

    private void SkipTrivia()
    {
        while (Match(TokenType.Whitespace, TokenType.Newline, TokenType.SingleLineComment, TokenType.MultiLineComment))
        {
        }
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
    private Token Previous() => _tokens[_current - 1];
    private bool IsAtEnd() => Current().Type == TokenType.EndOfFile;

    private static string StripQuotes(string s)
    {
        if (s.Length >= 2 && (s[0] == '"' || s[0] == '\'' || s[0] == '`'))
            return s[1..^1];
        return s;
    }
}

public class ImportDeclaration
{
    public string? DefaultImport
    {
        get; set;
    }
    public List<NamedImport>? NamedImports
    {
        get; set;
    }
    public string? NamespaceImport
    {
        get; set;
    }
    public string Source { get; set; } = string.Empty;
    public bool IsSideEffect
    {
        get; set;
    }
    public bool IsDynamic
    {
        get; set;
    }
    public int Line
    {
        get; set;
    }
    public int Column
    {
        get; set;
    }
}

public class NamedImport
{
    public required string Imported
    {
        get; set;
    }
    public required string Local
    {
        get; set;
    }
}

public class ExportDeclaration
{
    public bool IsDefault
    {
        get; set;
    }
    public bool IsReExport
    {
        get; set;
    }
    public bool All
    {
        get; set;
    }
    public string? Namespace
    {
        get; set;
    }
    public List<string>? Named
    {
        get; set;
    }
    public string? Source
    {
        get; set;
    }
    public int Line
    {
        get; set;
    }
    public int Column
    {
        get; set;
    }
}
