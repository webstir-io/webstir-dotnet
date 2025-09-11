using System;
using Engine.Pipelines.Css.Tokenization;
using Tests.Framework;

namespace Tests.Pipelines.Css;

public sealed class CssZeroShorthandCollapse : ITestCase
{
    public string Name => "CSS zero shorthands collapse on margin/padding/inset";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext _)
    {
        string css = ".m{margin:0 0 0 0}.p{padding:0 0 0}.i{inset:0 0}.ii{inset-inline:0 0}.ib{inset-block:0 0}\n.white{color:white}.b{background:black}.y{border-color:yellow}.f{outline-color:fuchsia}.t{color:transparent}.a{color:aqua}.l{color:lime}.g{background:linear-gradient(white, black)}";

        CssTokenizer tokenizer = new(css);
        System.Collections.Generic.List<CssToken> tokens = tokenizer.Tokenize(preserveLicenseComments: true);
        System.Collections.Generic.List<CssToken> minified = CssTokenMinifier.Minify(tokens);
        string output = CssSerializer.Serialize(minified);

        // Zero shorthand collapse
        Assert.Contains(".m{margin:0}", output, "margin 4 zeros should collapse to single 0");
        Assert.Contains(".p{padding:0}", output, "padding 3 zeros should collapse to single 0");
        Assert.Contains(".i{inset:0}", output, "inset 2 zeros should collapse to single 0");
        Assert.Contains(".ii{inset-inline:0}", output, "inset-inline 2 zeros should collapse to single 0");
        Assert.Contains(".ib{inset-block:0}", output, "inset-block 2 zeros should collapse to single 0");

        // Cautious color canonicalization
        Assert.Contains(".white{color:#fff}", output, "white should map to #fff in values only");
        Assert.Contains(".b{background:#000}", output, "black should map to #000");
        Assert.Contains(".y{border-color:#ff0}", output, "yellow should map to #ff0");
        Assert.Contains(".f{outline-color:#f0f}", output, "fuchsia should map to #f0f");
        Assert.Contains(".t{color:#0000}", output, "transparent should map to #0000");
        Assert.Contains(".a{color:#0ff}", output, "aqua should map to #0ff");
        Assert.Contains(".l{color:#0f0}", output, "lime should map to #0f0");

        // Guard: colors inside functions are not canonicalized
        Assert.Contains(".g{background:linear-gradient(white, black)}", output, "Color names inside functions should remain unchanged");
    }
}
