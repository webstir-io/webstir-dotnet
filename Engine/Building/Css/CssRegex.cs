using System.Text.RegularExpressions;

namespace Engine.Building.Css;

public static partial class CssRegex
{
    [GeneratedRegex(@"@import\s+(?:url\s*\()?\s*[""']([^""']+)[""']\s*\)?;")]
    public static partial Regex Import();
}