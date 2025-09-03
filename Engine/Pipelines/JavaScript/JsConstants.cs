namespace Engine.Pipelines.JavaScript;

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
}
