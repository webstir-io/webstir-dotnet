using System.Text.RegularExpressions;

namespace Engine.Pipelines.Css;

public static partial class CssRegex
{
    // Font-related patterns still in use by FontPreloadInjector
    [GeneratedRegex(@"@font-face[\s\S]*?\{[\s\S]*?src:\s*([^;]*);[\s\S]*?\}", RegexOptions.IgnoreCase)]
    public static partial Regex FontFaceWithSrc();

    [GeneratedRegex(@"src:\s*([^;]+);", RegexOptions.IgnoreCase)]
    public static partial Regex FontSrcDecl();

    [GeneratedRegex(@"url\((['""]?)(?<url>[^)'""]+)['""]?\)", RegexOptions.IgnoreCase)]
    public static partial Regex FontUrlExtractor();
}
