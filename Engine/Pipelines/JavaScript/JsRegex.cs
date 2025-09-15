using System.Text.RegularExpressions;

namespace Engine.Pipelines.JavaScript;

public static partial class JsRegex
{
    // Source map comment patterns
    [GeneratedRegex(@"^\s*\/\/\#\s*sourceMappingURL=.*$", RegexOptions.Multiline)]
    public static partial Regex SourceMapLine();

    [GeneratedRegex(@"\/\*\#\s*sourceMappingURL=.*?\*\/\s*$", RegexOptions.Singleline)]
    public static partial Regex SourceMapBlock();

    // TypeScript compilation error patterns
    [GeneratedRegex(@"^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\):\s*error\s+TS\d+:\s*(?<msg>.+)$", RegexOptions.Multiline)]
    public static partial Regex TscClassicError();

    [GeneratedRegex(@"^(?<file>.+?):(?<line>\d+):(?<col>\d+)\s*-\s*error\s+TS\d+:\s*(?<msg>.+)$", RegexOptions.Multiline)]
    public static partial Regex TscModernError();

    // Esbuild error pattern
    [GeneratedRegex(@"^(?<file>.+?):(?<line>\d+):(?<col>\d+):\s*(error|warning):\s*(?<msg>.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    public static partial Regex EsbuildError();
}
