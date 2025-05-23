using CLI.Bundlers.Javascript.Models;

namespace CLI.Bundlers.Javascript.Lexer;

public static class TokenPatterns
{
    public static readonly Dictionary<TokenType, string> Patterns = new()
    {
        // Keywords first (more specific than identifiers)
        { TokenType.IMPORT_KEYWORD, @"\bimport\b" },
        { TokenType.EXPORT_KEYWORD, @"\bexport\b" },
        { TokenType.FROM_KEYWORD, @"\bfrom\b" },
        { TokenType.FUNCTION_KEYWORD, @"\bfunction\b" },
        { TokenType.LET_KEYWORD, @"\blet\b" },
        { TokenType.CONST_KEYWORD, @"\bconst\b" },
        { TokenType.VAR_KEYWORD, @"\bvar\b" },
        { TokenType.RETURN_KEYWORD, @"\breturn\b" },
        { TokenType.IF_KEYWORD, @"\bif\b" },
        { TokenType.ELSE_KEYWORD, @"\belse\b" },
        { TokenType.FOR_KEYWORD, @"\bfor\b" },
        { TokenType.WHILE_KEYWORD, @"\bwhile\b" },
        { TokenType.DO_KEYWORD, @"\bdo\b" },
        { TokenType.SWITCH_KEYWORD, @"\bswitch\b" },
        { TokenType.CASE_KEYWORD, @"\bcase\b" },
        { TokenType.DEFAULT_KEYWORD, @"\bdefault\b" },
        { TokenType.BREAK_KEYWORD, @"\bbreak\b" },
        { TokenType.CONTINUE_KEYWORD, @"\bcontinue\b" },
        { TokenType.TRUE_KEYWORD, @"\btrue\b" },
        { TokenType.FALSE_KEYWORD, @"\bfalse\b" },
        { TokenType.NULL_KEYWORD, @"\bnull\b" },
        { TokenType.UNDEFINED_KEYWORD, @"\bundefined\b" },
        { TokenType.THROW_KEYWORD, @"\bthrow\b" },
        { TokenType.CATCH_KEYWORD, @"\bcatch\b" },
        { TokenType.TRY_KEYWORD, @"\btry\b" },
        { TokenType.FINALLY_KEYWORD, @"\bfinally\b" },
        { TokenType.AS_KEYWORD, @"\bas\b" },

        // Literals
        { TokenType.STRING_LITERAL, @"(""([^""\\]|\\.)*""|'([^'\\]|\\.)*')" },
        { TokenType.TEMPLATE_LITERAL, @"`([^`\\$]|\\.)*`" },
        { TokenType.UNKNOWN, @"(""([^""\\]|\\.)*|'([^'\\]|\\.)*)" }, // catch unterminated strings
        { TokenType.NUMERIC_LITERAL, @"\b(0[xX][0-9a-fA-F]+|0[bB][01]+|0[oO][0-7]+|\d+(\.\d+)?)\b" },

        // Specific multi-char operators
        { TokenType.EXPONENTIATION_ASSIGN, @"\*\*=" },
        { TokenType.EXPONENTIATION, @"\*\*" },
        { TokenType.ARROW_FUNCTION, @"=>" },
        { TokenType.STRICT_EQUAL, @"===" },
        { TokenType.STRICT_NOT_EQUAL, @"!==" },
        { TokenType.EQUAL, @"==" },
        { TokenType.NOT_EQUAL, @"!=" },
        { TokenType.LESS_THAN_EQUAL, @"<=" },
        { TokenType.GREATER_THAN_EQUAL, @">=" },
        { TokenType.INCREMENT, @"\+\+" },
        { TokenType.DECREMENT, @"--" },
        { TokenType.PLUS_ASSIGN, @"\+=" },
        { TokenType.MINUS_ASSIGN, @"-=" },
        { TokenType.MULTIPLY_ASSIGN, @"\*=" },
        { TokenType.DIVIDE_ASSIGN, @"/=" },
        { TokenType.MODULO_ASSIGN, @"%=" },
        { TokenType.LOGICAL_AND, @"&&" },
        { TokenType.LOGICAL_OR, @"\|\|" },

        { TokenType.LEFT_SHIFT_ASSIGN, @"<<=" },
        { TokenType.RIGHT_SHIFT_ASSIGN, @">>=" },
        { TokenType.UNSIGNED_RIGHT_SHIFT_ASSIGN, @">>>=" },
        { TokenType.LEFT_SHIFT, @"<<" },
        { TokenType.UNSIGNED_RIGHT_SHIFT, @">>>"},
        { TokenType.RIGHT_SHIFT, @">>" },
        { TokenType.BITWISE_AND_ASSIGN, @"&=" },
        { TokenType.BITWISE_OR_ASSIGN, @"\|=" },
        { TokenType.BITWISE_XOR_ASSIGN, @"\^=" },
        { TokenType.BITWISE_XOR_ASSIGN, @"\^=" },
        { TokenType.NULL_COALESCE_ASSIGN, @"\?\?=" },
        { TokenType.NULL_COALESCE, @"\?\?" },
        { TokenType.SPREAD, @"\.{3}" },

        // Single-char Operators
        { TokenType.PLUS, @"\+" },
        { TokenType.MINUS, @"-" },
        { TokenType.ASSIGN, @"=" },
        { TokenType.DOT, @"(?<!\d)\.(?!\d)" },

        // Additional single-char operators/punctuation
        { TokenType.OPEN_BRACE, @"\{" },
        { TokenType.CLOSE_BRACE, @"\}" },
        { TokenType.OPEN_PAREN, @"\(" },
        { TokenType.CLOSE_PAREN, @"\)" },
        { TokenType.COMMA, @"," },
        { TokenType.SEMICOLON, @";" },

        // Identifiers after keywords to prevent keyword matching as identifiers
        { TokenType.IDENTIFIER, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b" },

        // Comments
        { TokenType.SINGLE_LINE_COMMENT, @"//.*?(?=\r?$)" },
        { TokenType.MULTI_LINE_COMMENT, @"/\*.*?\*/" },

        // Whitespace
        { TokenType.WHITESPACE, @"\s+" },

        // Missing punctuation
        { TokenType.MULTIPLY, @"\*" },
        { TokenType.DIVIDE, @"/" },
        { TokenType.MODULO, @"%" },
        { TokenType.LOGICAL_NOT, @"!" },
        { TokenType.BITWISE_AND, @"&" },
        { TokenType.BITWISE_OR, @"\|" },
        { TokenType.BITWISE_XOR, @"\^" },
        { TokenType.BITWISE_NOT, @"~" },
        { TokenType.LESS_THAN, @"<" },
        { TokenType.GREATER_THAN, @">" },
        { TokenType.OPEN_BRACKET, @"\[" },
        { TokenType.CLOSE_BRACKET, @"\]" },
        { TokenType.COLON, @":" },
        { TokenType.QUESTION_MARK, @"\?" },
    };
}