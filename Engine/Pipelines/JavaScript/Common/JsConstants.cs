namespace Engine.Pipelines.JavaScript.Common;

public static class Js
{
    public const string ModulePrefix = "__module_";
    public const string DefaultSuffix = "_default";
    public const string Const = "const";
    public const string Var = "var";
    public const string Import = "import";
    public const string Export = "export";
    public const string ExportDefault = "export default";
    public const string From = "from";
    public const string As = "as";
    public const string CommentPrefix = "//";
    public static string GetModuleVar(int moduleId) => $"{ModulePrefix}{moduleId}";
    public static string GetModuleDefault(int moduleId) => $"{ModulePrefix}{moduleId}{DefaultSuffix}";
    public static string GetModuleExport(int moduleId, string name) => $"{ModulePrefix}{moduleId}_{name}";

}

public static class Prefixes
{
    public const string Node = "node:";
    public const string Relative = "./";
    public const string ParentRelative = "../";
}

public static class Exts
{
    public const string TypeScript = ".ts";
    public const string JavaScript = ".js";
    public const string ModuleJs = ".mjs";
    public const string Json = ".json";
}

public static class PackageJsonFields
{
    public const string Main = "main";
    public const string Module = "module";
}

public static class Syntax
{
    // String constants (existing)
    public const string OpenBrace = "{";
    public const string CloseBrace = "}";
    public const string OpenParen = "(";
    public const string CloseParen = ")";
    public const string Semicolon = ";";
    public const string Quote = "'";
    public const string Comma = ", ";
    public const string Newline = "\n";
    public const string Space = " ";
    public const string Asterisk = "*";
    public const string Assignment = " = ";

    // Character constants for minification and parsing
    public const char SlashChar = '/';
    public const char BackslashChar = '\\';
    public const char SingleQuoteChar = '\'';
    public const char DoubleQuoteChar = '"';
    public const char BacktickChar = '`';
    public const char DollarChar = '$';
    public const char OpenBraceChar = '{';
    public const char CloseBraceChar = '}';
    public const char OpenBracketChar = '[';
    public const char CloseBracketChar = ']';
    public const char OpenParenChar = '(';
    public const char CloseParenChar = ')';
    public const char AsteriskChar = '*';
    public const char ExclamationChar = '!';
    public const char NewlineChar = '\n';
    public const char CarriageReturnChar = '\r';
    public const char SpaceChar = ' ';
    public const char PlusChar = '+';
    public const char MinusChar = '-';
    public const char CommaChar = ',';
    public const char SemicolonChar = ';';
    public const char ColonChar = ':';
    public const char QuestionChar = '?';
    public const char EqualsChar = '=';
    public const char LessThanChar = '<';
    public const char GreaterThanChar = '>';
    public const char PercentChar = '%';
    public const char AmpersandChar = '&';
    public const char PipeChar = '|';
    public const char CaretChar = '^';
    public const char TildeChar = '~';
    public const char NullChar = '\0';
}
