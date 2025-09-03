using System.Text.RegularExpressions;

namespace Engine.Pipelines.Css;

public static partial class CssRegex
{
    [GeneratedRegex(@"@import\s+(?:url\()?['""]?([^'"")\s]+)['""]?\)?(?:\s+([^;]+))?;", RegexOptions.Multiline)]
    public static partial Regex Import();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    public static partial Regex BlockComment();

    [GeneratedRegex(@"\.([a-zA-Z_][\w-]*)", RegexOptions.Multiline)]
    public static partial Regex ClassSelector();

    [GeneratedRegex(@"url\(['""]?([^'"")\s]+)['""]?\)", RegexOptions.Multiline)]
    public static partial Regex Url();

    [GeneratedRegex(@"\s+", RegexOptions.Multiline)]
    public static partial Regex Whitespace();

    [GeneratedRegex(@";\s*}", RegexOptions.Multiline)]
    public static partial Regex LastSemicolon();

    [GeneratedRegex(@":\s+", RegexOptions.Multiline)]
    public static partial Regex ColonSpace();

    [GeneratedRegex(@"\s*([{}:;,>+~])\s*", RegexOptions.Multiline)]
    public static partial Regex OperatorSpace();

    [GeneratedRegex(@"#([0-9a-fA-F])\1([0-9a-fA-F])\2([0-9a-fA-F])\3", RegexOptions.IgnoreCase)]
    public static partial Regex HexColor();

    [GeneratedRegex(@"\b0(\.\d+)", RegexOptions.Multiline)]
    public static partial Regex LeadingZero();

    [GeneratedRegex(@"(\d+)\.0+(?![0-9])", RegexOptions.Multiline)]
    public static partial Regex TrailingZero();

    [GeneratedRegex(@"\b0(px|em|%|in|cm|mm|pc|pt|ex)", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    public static partial Regex ZeroUnit();

    [GeneratedRegex(@"\{([^}]*)\}", RegexOptions.Singleline)]
    public static partial Regex RuleBlock();

    [GeneratedRegex(@"([a-z-]+)\s*:\s*([^;]+);", RegexOptions.Multiline)]
    public static partial Regex Property();
}
