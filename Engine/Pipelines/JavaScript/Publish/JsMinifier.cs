using System;
using System.Text;
using Engine.Extensions;

namespace Engine.Pipelines.JavaScript.Publish;

public static class JsMinifier
{
    private enum MinifyMode
    {
        Code,
        SingleQuote,
        DoubleQuote,
        Template,
        TemplateExpr,
        Regex
    }

    public static string Minify(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (code.Length == 0)
        {
            return string.Empty;
        }

        string result = new JsMinifyEngine(code).Run();

        // Strip source map comments (`//# sourceMappingURL=...` and `/*# sourceMappingURL=... */`)
        result = JsRegex.SourceMapLine().Replace(result, string.Empty);
        result = JsRegex.SourceMapBlock().Replace(result, string.Empty);

        return result.Trim();
    }

    private sealed class JsMinifyEngine(string code)
    {
        private readonly string _code = code;
        private readonly int _length = code.Length;
        private readonly StringBuilder _output = new(code.Length);

        private MinifyMode _mode = MinifyMode.Code;
        private int _index;
        private int _templateExprDepth;
        private bool _inCharacterClass;
        private char _lastNonWhitespaceCharacter = '\0';

        public string Run()
        {
            while (_index < _length)
            {
                char currentChar = _code[_index];

                if (_mode == MinifyMode.Code)
                {
                    if (ProcessWhitespace())
                    {
                        continue;
                    }

                    if (ProcessSlash())
                    {
                        continue;
                    }

                    if (ProcessQuoteOrTemplateStart(currentChar))
                    {
                        continue;
                    }

                    Emit(currentChar);
                    _index++;
                    continue;
                }

                if (_mode == MinifyMode.SingleQuote)
                {
                    HandleSingleQuote();
                    continue;
                }

                if (_mode == MinifyMode.DoubleQuote)
                {
                    HandleDoubleQuote();
                    continue;
                }

                if (_mode == MinifyMode.Template)
                {
                    HandleTemplate();
                    continue;
                }

                if (_mode == MinifyMode.TemplateExpr)
                {
                    HandleTemplateExpression();
                    continue;
                }

                if (_mode == MinifyMode.Regex)
                {
                    HandleRegex();
                    continue;
                }
            }

            return _output.ToString();
        }

        // whitespace scanning moved to TextScanner.ScanAsciiWhitespace

        private void EmitRange(int startInclusive, int endExclusive)
        {
            for (int i = startInclusive; i < endExclusive; i++)
            {
                Emit(_code[i]);
            }
        }

        private void SkipLineComment()
        {
            int scanIndex = _index + 2;
            while (scanIndex < _length && !IsLineTerminator(_code[scanIndex]))
            {
                scanIndex++;
            }
            _index = scanIndex;
        }

        private (bool isLicense, int endExclusive) ScanBlockComment()
        {
            int scanIndex = _index + 2;
            bool isLicense = scanIndex < _length && _code[scanIndex] == '!';

            int blockCommentEnd = scanIndex;
            while (blockCommentEnd + 1 < _length && !(_code[blockCommentEnd] == '*' && _code[blockCommentEnd + 1] == '/'))
            {
                blockCommentEnd++;
            }

            int endExclusive = Math.Min(blockCommentEnd + 2, _length);
            return (isLicense, endExclusive);
        }

        private bool TryEmitEscape() => TextScanner.TryEmitEscape(_code, ref _index, Emit);

        private bool ProcessWhitespace()
        {
            char currentChar = _code[_index];
            if (!IsWhitespace(currentChar))
            {
                return false;
            }

            (bool sawNewline, char nextNonWhitespaceChar, int nextIndex) = TextScanner.ScanAsciiWhitespace(_code, _index);

            if (sawNewline)
            {
                Emit('\n');
            }
            else
            {
                if (nextNonWhitespaceChar != '\0' && ShouldInsertSpace(_lastNonWhitespaceCharacter, nextNonWhitespaceChar))
                {
                    Emit(' ');
                }
            }

            _index = nextIndex;
            return true;
        }

        private bool ProcessSlash()
        {
            char currentChar = _code[_index];
            if (currentChar != '/')
            {
                return false;
            }

            if (_index + 1 < _length)
            {
                char firstLookaheadChar = _code[_index + 1];

                if (firstLookaheadChar == '/')
                {
                    SkipLineComment();
                    return true;
                }

                if (firstLookaheadChar == '*')
                {
                    (bool isLicense, int endExclusive) = ScanBlockComment();
                    if (isLicense)
                    {
                        EmitRange(_index, endExclusive);
                    }

                    _index = endExclusive;
                    return true;
                }

                if (ShouldStartRegex())
                {
                    Emit('/');
                    _index++;
                    _mode = MinifyMode.Regex;
                    _inCharacterClass = false;
                    return true;
                }
            }

            Emit('/');
            _index++;
            return true;
        }

        private bool ProcessQuoteOrTemplateStart(char currentChar)
        {
            if (currentChar == '\'')
            {
                Emit(currentChar);
                _index++;
                _mode = MinifyMode.SingleQuote;
                return true;
            }

            if (currentChar == '"')
            {
                Emit(currentChar);
                _index++;
                _mode = MinifyMode.DoubleQuote;
                return true;
            }

            if (currentChar == '`')
            {
                Emit(currentChar);
                _index++;
                _mode = MinifyMode.Template;
                return true;
            }

            return false;
        }

        private void HandleSingleQuote() => HandleQuotedLiteral('\'');

        private void HandleDoubleQuote() => HandleQuotedLiteral('"');

        private void HandleQuotedLiteral(char quote)
        {
            ReadQuotedString(quote);
            _mode = MinifyMode.Code;
        }

        private void HandleTemplate()
        {
            if (TryEmitEscape())
            {
                return;
            }

            char currentChar = _code[_index];
            Emit(currentChar);
            _index++;

            if (currentChar == '`')
            {
                _mode = MinifyMode.Code;
                return;
            }

            if (currentChar == '$' && _index < _length && _code[_index] == '{')
            {
                Emit('{');
                _index++;
                _mode = MinifyMode.TemplateExpr;
                _templateExprDepth = 1;
            }
        }

        private void HandleTemplateExpression()
        {
            if (ProcessWhitespace())
            {
                return;
            }

            if (ProcessSlash())
            {
                return;
            }

            if (TryEmitEscape())
            {
                return;
            }

            char currentChar = _code[_index];

            if (currentChar == '\'') { ReadQuotedString('\''); return; }
            if (currentChar == '"') { ReadQuotedString('"'); return; }

            if (currentChar == '`')
            {
                Emit(currentChar);
                _index++;
                _mode = MinifyMode.Template;
                return;
            }

            if (currentChar == '{')
            {
                Emit(currentChar);
                _templateExprDepth++;
                _index++;
                return;
            }

            if (currentChar == '}')
            {
                Emit(currentChar);
                _templateExprDepth--;
                _index++;
                if (_templateExprDepth == 0)
                {
                    _mode = MinifyMode.Template;
                }
                return;
            }

            Emit(currentChar);
            _index++;
        }

        private void ReadQuotedString(char quote) => TextScanner.ReadQuotedString(_code, ref _index, quote, Emit);

        private void HandleRegex()
        {
            if (TryEmitEscape())
            {
                return;
            }

            char currentChar = _code[_index];

            if (currentChar == '[')
            {
                _inCharacterClass = true;
                Emit(currentChar);
                _index++;
                return;
            }

            if (currentChar == ']' && _inCharacterClass)
            {
                _inCharacterClass = false;
                Emit(currentChar);
                _index++;
                return;
            }

            if (currentChar == '/' && !_inCharacterClass)
            {
                Emit('/');
                _index++;

                while (_index < _length)
                {
                    char flagChar = _code[_index];
                    if (flagChar is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
                    {
                        Emit(flagChar);
                        _index++;
                    }
                    else
                    {
                        break;
                    }
                }

                _mode = MinifyMode.Code;
                return;
            }

            Emit(currentChar);
            _index++;
        }

        private static bool IsWhitespace(char character) => character.IsAsciiWhitespace();

        private static bool IsLineTerminator(char character) => character.IsLineTerminator();

        private static bool IsIdentifierPart(char character) => char.IsLetterOrDigit(character) || character == '_' || character == '$';

        private static bool EndsWithKeyword(StringBuilder builder, string keyword) => builder.EndsWithToken(keyword, IsIdentifierPart);

        private static bool ShouldInsertSpace(char previousChar, char nextChar)
        {
            if (previousChar == '\0')
            {
                return false;
            }

            bool prevIsIdent = IsIdentifierPart(previousChar);
            bool nextIsIdent = IsIdentifierPart(nextChar);
            if (prevIsIdent && nextIsIdent)
            {
                return true;
            }

            if ((previousChar == '+' && nextChar == '+') || (previousChar == '-' && nextChar == '-'))
            {
                return true;
            }

            if ((char.IsDigit(previousChar) && nextIsIdent) || (prevIsIdent && char.IsDigit(nextChar)))
            {
                return true;
            }

            return false;
        }

        private static bool IsBeforeRegexByChar(char previousChar)
        {
            return previousChar is '\0'
                or '(' or '{' or '['
                or ',' or ';' or ':' or '?'
                or '=' or '!' or '<' or '>'
                or '+' or '-' or '*' or '%'
                or '&' or '|' or '^' or '~'
                or '\n' or '\r';
        }

        private static bool EndsWithRegexKeyword(StringBuilder builder) => builder.EndsWithAnyToken(IsIdentifierPart, JsKeywords.RegexPrefixKeywords);

        private bool ShouldStartRegex() => IsBeforeRegexByChar(_lastNonWhitespaceCharacter)
            || EndsWithRegexKeyword(_output);

        private void Emit(char character)
        {
            _output.Append(character);
            if (!IsWhitespace(character))
            {
                _lastNonWhitespaceCharacter = character;
            }
        }
    }
}
