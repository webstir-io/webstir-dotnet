namespace CLI.Bundlers.Javascript.Models;

public class LexerException : Exception
{
    public LexerException(string message, int line, int column)
        : base($"Lexer error at {line}:{column} - {message}") { }
}