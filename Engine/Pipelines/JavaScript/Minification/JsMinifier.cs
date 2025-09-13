using System;
using Engine.Pipelines.JavaScript.Common;

namespace Engine.Pipelines.JavaScript.Minification;

public static class JsMinifier
{
    public static string Minify(string code, bool compact = false, bool dropConsole = false)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (code.Length == 0)
        {
            return string.Empty;
        }

        string result = new JsMinifyProcessor(code, compact).Run();

        // Strip source map comments (`//# sourceMappingURL=...` and `/*# sourceMappingURL=... */`)
        result = JsRegex.SourceMapLine().Replace(result, string.Empty);
        result = JsRegex.SourceMapBlock().Replace(result, string.Empty);

        if (dropConsole)
        {
            // Conservative removal of console.* calls and debugger statements
            result = JsRegex.DebuggerStatement().Replace(result, string.Empty);
            result = JsRegex.ConsoleCall().Replace(result, string.Empty);
        }

        if (compact)
        {
            result = result.Replace("\n", string.Empty);
            result = JsRegex.PunctWhitespaceRun().Replace(result, string.Empty);
        }

        return result.Trim();
    }
}
