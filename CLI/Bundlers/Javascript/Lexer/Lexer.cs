using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CLI.Bundlers.Javascript.Models;

namespace CLI.Bundlers.Javascript.Lexer;

public class Lexer
{
    public List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        int position = 0, line = 1, column = 1;

        while (position < input.Length)
        {
            Token? match = null;
            foreach (var pattern in TokenPatterns.Patterns)
            {
                var regex = new Regex("^" + pattern.Value, RegexOptions.Singleline | RegexOptions.Compiled);
                var matchResult = regex.Match(input.Substring(position));

                if (matchResult.Success)
                {
                    match = new Token(pattern.Key, matchResult.Value, line, column);
                    UpdatePosition(matchResult.Value, ref line, ref column);
                    position += matchResult.Length;
                    break;
                }
            }

            if (match == null)
            {
                char invalidChar = input[position];
                string errorMsg = char.IsControl(invalidChar) ?
                    $"Invalid control character (code {((int)invalidChar)}) encountered." :
                    $"Invalid character '{invalidChar}' encountered.";
                
                throw new LexerException(errorMsg, line, column);
            }

            if (match.Type != TokenType.WHITESPACE && match.Type != TokenType.SINGLE_LINE_COMMENT && match.Type != TokenType.MULTI_LINE_COMMENT)
                tokens.Add(match);
        }

        tokens.Add(new Token(TokenType.EOF, string.Empty, line, column));
        return tokens;
    }

    private void UpdatePosition(string matchedValue, ref int line, ref int column)
    {
        foreach (var c in matchedValue)
        {
            if (c == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }
    }
}
