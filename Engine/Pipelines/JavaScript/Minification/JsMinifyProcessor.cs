using System.Text;
using Engine.Extensions;
using static Engine.Pipelines.JavaScript.Common.Syntax;

namespace Engine.Pipelines.JavaScript.Minification;

internal sealed class JsMinifyProcessor(string code)
{
    private readonly string _code = code;
    private readonly int _length = code.Length;
    private readonly StringBuilder _output = new(code.Length);
    private readonly MinifyState _state = new();

    public string Run()
    {
        while (_state.Index < _length)
        {
            switch (_state.Mode)
            {
                case MinifyMode.Code:
                    ProcessCodeMode();
                    break;
                case MinifyMode.SingleQuote:
                    HandleSingleQuote();
                    break;
                case MinifyMode.DoubleQuote:
                    HandleDoubleQuote();
                    break;
                case MinifyMode.Template:
                    HandleTemplate();
                    break;
                case MinifyMode.TemplateExpr:
                    HandleTemplateExpression();
                    break;
                case MinifyMode.Regex:
                    HandleRegex();
                    break;
            }
        }

        return _output.ToString();
    }

    private void ProcessCodeMode()
    {
        if (ProcessWhitespace())
        {
            return;
        }

        if (ProcessSlash())
        {
            return;
        }

        char currentChar = _code[_state.Index];
        if (ProcessQuoteOrTemplateStart(currentChar))
        {
            return;
        }

        Emit(currentChar);
        _state.Index++;
    }

    private bool ProcessWhitespace()
    {
        char currentChar = _code[_state.Index];
        if (!CharacterClassifier.IsWhitespace(currentChar))
        {
            return false;
        }

        (bool sawNewline, char nextNonWhitespaceChar, int nextIndex) = TextScanner.ScanAsciiWhitespace(_code, _state.Index);

        if (sawNewline)
        {
            Emit(NewlineChar);
        }
        else
        {
            if (nextNonWhitespaceChar != '\0' && CharacterClassifier.ShouldInsertSpace(_state.LastNonWhitespaceChar, nextNonWhitespaceChar))
            {
                Emit(SpaceChar);
            }
        }

        _state.Index = nextIndex;
        return true;
    }

    private bool ProcessSlash()
    {
        char currentChar = _code[_state.Index];
        if (currentChar != SlashChar)
        {
            return false;
        }

        if (_state.Index + 1 < _length)
        {
            char firstLookaheadChar = _code[_state.Index + 1];

            if (firstLookaheadChar == SlashChar)
            {
                CommentScanner.SkipLineComment(_code, _state);
                return true;
            }

            if (firstLookaheadChar == AsteriskChar)
            {
                (bool isLicense, int endExclusive) = CommentScanner.ScanBlockComment(_code, _state);
                if (isLicense)
                {
                    EmitRange(_state.Index, endExclusive);
                }

                _state.Index = endExclusive;
                return true;
            }

            if (RegexDetector.ShouldStartRegex(_state.LastNonWhitespaceChar, _output))
            {
                Emit(SlashChar);
                _state.Index++;
                _state.Mode = MinifyMode.Regex;
                _state.InCharacterClass = false;
                return true;
            }
        }

        Emit(SlashChar);
        _state.Index++;
        return true;
    }

    private bool ProcessQuoteOrTemplateStart(char currentChar)
    {
        if (currentChar == SingleQuoteChar)
        {
            Emit(currentChar);
            _state.Index++;
            _state.Mode = MinifyMode.SingleQuote;
            return true;
        }

        if (currentChar == DoubleQuoteChar)
        {
            Emit(currentChar);
            _state.Index++;
            _state.Mode = MinifyMode.DoubleQuote;
            return true;
        }

        if (currentChar == BacktickChar)
        {
            Emit(currentChar);
            _state.Index++;
            _state.Mode = MinifyMode.Template;
            return true;
        }

        return false;
    }

    private void HandleSingleQuote() => HandleQuotedLiteral(SingleQuoteChar);

    private void HandleDoubleQuote() => HandleQuotedLiteral(DoubleQuoteChar);

    private void HandleQuotedLiteral(char quote)
    {
        ReadQuotedString(quote);
        _state.Mode = MinifyMode.Code;
    }

    private void HandleTemplate()
    {
        if (TryEmitEscape())
        {
            return;
        }

        char currentChar = _code[_state.Index];
        Emit(currentChar);
        _state.Index++;

        if (currentChar == BacktickChar)
        {
            _state.Mode = MinifyMode.Code;
            return;
        }

        if (currentChar == DollarChar && _state.Index < _length && _code[_state.Index] == OpenBraceChar)
        {
            Emit(OpenBraceChar);
            _state.Index++;
            _state.Mode = MinifyMode.TemplateExpr;
            _state.TemplateExprDepth = 1;
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

        char currentChar = _code[_state.Index];

        if (currentChar == SingleQuoteChar)
        {
            ReadQuotedString(SingleQuoteChar);
            return;
        }
        if (currentChar == DoubleQuoteChar)
        {
            ReadQuotedString(DoubleQuoteChar);
            return;
        }

        if (currentChar == BacktickChar)
        {
            Emit(currentChar);
            _state.Index++;
            _state.Mode = MinifyMode.Template;
            return;
        }

        if (currentChar == OpenBraceChar)
        {
            Emit(currentChar);
            _state.TemplateExprDepth++;
            _state.Index++;
            return;
        }

        if (currentChar == CloseBraceChar)
        {
            Emit(currentChar);
            _state.TemplateExprDepth--;
            _state.Index++;
            if (_state.TemplateExprDepth == 0)
            {
                _state.Mode = MinifyMode.Template;
            }
            return;
        }

        Emit(currentChar);
        _state.Index++;
    }

    private void HandleRegex()
    {
        if (TryEmitEscape())
        {
            return;
        }

        char currentChar = _code[_state.Index];

        if (currentChar == OpenBracketChar)
        {
            _state.InCharacterClass = true;
            Emit(currentChar);
            _state.Index++;
            return;
        }

        if (currentChar == CloseBracketChar && _state.InCharacterClass)
        {
            _state.InCharacterClass = false;
            Emit(currentChar);
            _state.Index++;
            return;
        }

        if (currentChar == SlashChar && !_state.InCharacterClass)
        {
            Emit(SlashChar);
            _state.Index++;

            while (_state.Index < _length)
            {
                char flagChar = _code[_state.Index];
                if (flagChar is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
                {
                    Emit(flagChar);
                    _state.Index++;
                }
                else
                {
                    break;
                }
            }

            _state.Mode = MinifyMode.Code;
            return;
        }

        Emit(currentChar);
        _state.Index++;
    }

    private void EmitRange(int startInclusive, int endExclusive)
    {
        for (int i = startInclusive; i < endExclusive; i++)
        {
            Emit(_code[i]);
        }
    }


    private void ReadQuotedString(char quote)
    {
        int index = _state.Index;
        TextScanner.ReadQuotedString(_code, ref index, quote, Emit);
        _state.Index = index;
    }

    private bool TryEmitEscape()
    {
        int index = _state.Index;
        bool result = TextScanner.TryEmitEscape(_code, ref index, Emit);
        _state.Index = index;
        return result;
    }


    private void Emit(char character)
    {
        _output.Append(character);
        if (!CharacterClassifier.IsWhitespace(character))
        {
            _state.LastNonWhitespaceChar = character;
        }
    }
}
