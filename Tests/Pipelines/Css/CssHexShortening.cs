using System;
using Engine.Pipelines.Css.Minification;
using Tests.Framework;

namespace Tests.Pipelines.Css;

public sealed class CssHexShortening : ITestCase
{
    public string Name => "CSS hex shortener supports #rrggbb and #rrggbbaa";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext _)
    {
        string css = ".x{color:#aabbcc}.y{color:#11223344}.z{color:#AaBbCcDd}.k{color:#abc}.m{color:#1234}";

        CssTokenizer tokenizer = new(css);
        System.Collections.Generic.List<CssToken> tokens = tokenizer.Tokenize(preserveLicenseComments: true);
        System.Collections.Generic.List<CssToken> minified = CssTokenMinifier.Minify(tokens);
        string output = CssSerializer.Serialize(minified);

        Assert.Contains(".x{color:#abc}", output, "#rrggbb should shorten to #rgb when reducible");
        Assert.Contains(".y{color:#1234}", output, "#rrggbbaa should shorten to #rgba when reducible");
        Assert.Contains(".z{color:#abcd}", output, "#rrggbbaa mixed case should shorten to #rgba (lowercased)");
        Assert.Contains(".k{color:#abc}", output, "#rgb remains #rgb");
        Assert.Contains(".m{color:#1234}", output, "#rgba remains #rgba");
    }
}

