using System;
using System.Linq;
using CLI.Bundlers.Javascript.Lexer;
using CLI.Bundlers.Javascript.Models;

namespace CLI.Bundlers.Javascript.Lexer
{
    class LexerTests
    {
        static void Main()
        {
            RunAllTests();
        }

        static void RunAllTests()
        {
            Test("Simple Import", "import { example } from './module';", new[]
            {
                TokenType.IMPORT_KEYWORD, TokenType.OPEN_BRACE, TokenType.IDENTIFIER, TokenType.CLOSE_BRACE,
                TokenType.FROM_KEYWORD, TokenType.STRING_LITERAL, TokenType.SEMICOLON, TokenType.EOF
            });

            Test("Numeric Literals", "100 0xFF 0b1010", new[]
            {
                TokenType.NUMERIC_LITERAL, TokenType.NUMERIC_LITERAL, TokenType.NUMERIC_LITERAL, TokenType.EOF
            });

            Test("Operators", "x += 5 **= y && z || w;", new[]
            {
                TokenType.IDENTIFIER, TokenType.PLUS_ASSIGN, TokenType.NUMERIC_LITERAL, TokenType.EXPONENTIATION_ASSIGN,
                TokenType.IDENTIFIER, TokenType.LOGICAL_AND, TokenType.IDENTIFIER, TokenType.LOGICAL_OR,
                TokenType.IDENTIFIER, TokenType.SEMICOLON, TokenType.EOF
            });

            Test("String and Template Literals", "`template ${expr}` \"string\" 'string'", new[]
            {
                TokenType.TEMPLATE_LITERAL, TokenType.UNKNOWN, TokenType.IDENTIFIER, TokenType.UNKNOWN,
                TokenType.STRING_LITERAL, TokenType.STRING_LITERAL, TokenType.EOF
            });

            Test("Comments Handling", "//comment\nvar x;", new[]
            {
                TokenType.VAR_KEYWORD, TokenType.IDENTIFIER, TokenType.SEMICOLON, TokenType.EOF
            });

            Test("Arrow Function", "(x) => x * x;", new[]
            {
                TokenType.OPEN_PAREN, TokenType.IDENTIFIER, TokenType.CLOSE_PAREN, TokenType.ARROW_FUNCTION,
                TokenType.IDENTIFIER, TokenType.MULTIPLY, TokenType.IDENTIFIER, TokenType.SEMICOLON, TokenType.EOF
            });

            Console.WriteLine("\nAll tests completed.");
        }

        static void Test(string testName, string input, TokenType[] expectedTypes)
        {
            var lexer = new Lexer();
            try
            {
                var tokens = lexer.Tokenize(input);
                var actualTypes = tokens.Select(t => t.Type).ToArray();

                if (actualTypes.SequenceEqual(expectedTypes))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[PASS] {testName}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[FAIL] {testName} - Expected: {string.Join(", ", expectedTypes)}, Got: {string.Join(", ", actualTypes)}");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {testName} - Exception: {ex.Message}");
            }
            finally
            {
                Console.ResetColor();
            }
        }
    }
}