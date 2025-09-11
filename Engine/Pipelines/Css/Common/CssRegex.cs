using System.Text.RegularExpressions;

namespace Engine.Pipelines.Css.Common;

public static partial class CssRegex
{
    [GeneratedRegex(@"@import\s+(?:url\()?['""]?([^'"")\s]+)['""]?\)?(?:\s+([^;]+))?;", RegexOptions.Multiline)]
    public static partial Regex Import();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    public static partial Regex BlockComment();

    // Matches block comments that are NOT important license comments (/*! ... */)
    [GeneratedRegex(@"/\*(?!\!).*?\*/", RegexOptions.Singleline)]
    public static partial Regex NonImportantBlockComment();

    [GeneratedRegex(@"\.([a-zA-Z_][\w-]*)", RegexOptions.Multiline)]
    public static partial Regex ClassSelector();

    // Matches url(...) capturing either quoted or unquoted contents
    // Groups: 1=quote (' or "), 2=quoted inner, 3=unquoted inner
    [GeneratedRegex(@"url\(\s*(?:(['""])\s*(.*?)\s*\1|([^)]*))\s*\)", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
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

    [GeneratedRegex(@"\b0(px|em|rem|%|in|cm|mm|pc|pt|ex|vh|vw|vmin|vmax|ch)", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    public static partial Regex ZeroUnit();

    [GeneratedRegex(@"\{([^}]*)\}", RegexOptions.Singleline)]
    public static partial Regex RuleBlock();

    [GeneratedRegex(@"([a-z-]+)\s*:\s*([^;]+);", RegexOptions.Multiline)]
    public static partial Regex Property();
}
