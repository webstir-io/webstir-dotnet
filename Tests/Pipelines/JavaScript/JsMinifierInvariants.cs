using System;
using Engine.Pipelines.JavaScript.Minification;
using Tests.Framework;

namespace Tests.Pipelines.JavaScript;

public sealed class JsMinifierInvariants : ITestCase
{
    public string Name => "JS minifier preserves strings/templates/regex and license";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        string input =
            "/*! license comment */\n" +
            "// leading line comment should be removed\n" +
            "const s1 = 'a\\\'b'; /* regular block */\n" +
            "const s2 = \"a\\\"b\"; // trailing line\n" +
            "const t = `tmpl ${1 + 2} end`;\n" +
            "const r1 = /a[^]b/g;\n" +
            "function f(a,b){ return /re/.test(a) ? a / b / 2 : a++/b }\n" +
            "//# sourceMappingURL=app.js.map\n" +
            "/*# sourceMappingURL=app.js.map */\n";

        string minified = JsMinifier.Minify(input);

        // License is preserved
        Assert.Contains("/*! license comment */", minified, "License comment should be preserved");

        // Non-license comments removed
        Assert.DoesNotContain("regular block", minified, "Regular block comments should be removed");
        Assert.DoesNotContain("leading line comment", minified, "Line comments should be removed");

        // Source map comments removed
        Assert.DoesNotContain("sourceMappingURL", minified, "sourceMappingURL comments should be stripped");

        // Strings preserved
        Assert.Contains("'a\\'b'", minified, "Single-quoted string should be intact");
        Assert.Contains("\"a\\\"b\"", minified, "Double-quoted string should be intact");

        // Template literal preserved (outer structure)
        Assert.Contains("`tmpl ${1 + 2} end`".Replace(" ", string.Empty), minified.Replace(" ", string.Empty), "Template literal structure should be present");

        // Regex bodies preserved
        Assert.Contains("/a[^]b/g", minified, "Regex literal body should be preserved");
        Assert.Contains("/re/.test(a)", minified, "Regex literal in return should be preserved");

        // Division should remain division
        Assert.Contains("a++/b", minified, "Postfix ++ followed by division should be intact");
    }
}
