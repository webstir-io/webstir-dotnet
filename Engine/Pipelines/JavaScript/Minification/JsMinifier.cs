using System;
using Engine.Pipelines.JavaScript.Common;

namespace Engine.Pipelines.JavaScript.Minification;

public static class JsMinifier
{
    public static string Minify(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (code.Length == 0)
        {
            return string.Empty;
        }

        string result = new JsMinifyProcessor(code).Run();

        // Strip source map comments (`//# sourceMappingURL=...` and `/*# sourceMappingURL=... */`)
        result = JsRegex.SourceMapLine().Replace(result, string.Empty);
        result = JsRegex.SourceMapBlock().Replace(result, string.Empty);

        return result.Trim();
    }
}
