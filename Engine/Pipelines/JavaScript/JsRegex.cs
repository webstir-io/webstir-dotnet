using System.Text.RegularExpressions;

namespace Engine.Pipelines.JavaScript;

public static partial class JsRegex
{
    [GeneratedRegex(@"^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\):\s*error\s+TS\d+:\s*(?<msg>.+)$", RegexOptions.Multiline)]
    public static partial Regex TscClassicError();

    [GeneratedRegex(@"^(?<file>.+?):(?<line>\d+):(?<col>\d+)\s*-\s*error\s+TS\d+:\s*(?<msg>.+)$", RegexOptions.Multiline)]
    public static partial Regex TscModernError();
}
