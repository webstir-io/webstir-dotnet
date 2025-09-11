using System;
using System.Linq;
using Engine.Pipelines.Css.Minification;
using Tests.Framework;

namespace Tests.Pipelines.Css;

public sealed class CssTokenizerUnit : ITestCase
{
    public string Name => "Tokenizer identifies strings/urls and preserves license comments";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext _)
    {
        string css = "/*! lic */ .a{content:\" : ; {} \";background-image:url(/images/a b.png)} /* drop */ .b{margin:0rem}";

        CssTokenizer tokenizer = new(css);
        System.Collections.Generic.List<CssToken> tokens = tokenizer.Tokenize(preserveLicenseComments: true);

        Assert.IsTrue(tokens.Any(token => token.Type == CssTokenType.Comment && token.Value.StartsWith("/*! lic */", StringComparison.Ordinal)),
            "Expected preserved license comment token");

        Assert.IsTrue(tokens.Any(token => token.Type == CssTokenType.String && token.Value.Contains(" : ; {} ", StringComparison.Ordinal)),
            "Expected string token with punctuation content");

        Assert.IsTrue(tokens.Any(token => token.Type == CssTokenType.Url && token.Value.StartsWith("url(", StringComparison.OrdinalIgnoreCase)),
            "Expected url() token");

        // Ensure non-important comments are not present
        Assert.IsFalse(tokens.Any(token => token.Type == CssTokenType.Comment && !token.Value.StartsWith("/*!", StringComparison.Ordinal)),
            "Non-important comments should be dropped by tokenizer");

        // Dimension token for 0rem
        Assert.IsTrue(tokens.Any(token => token.Type == CssTokenType.Dimension && token.Value.Equals("0rem", StringComparison.Ordinal)),
            "Expected dimension token for 0rem");
    }
}
