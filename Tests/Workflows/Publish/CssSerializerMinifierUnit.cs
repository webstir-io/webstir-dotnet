using System;
using Engine.Pipelines.Css.Tokenization;
using Tests.Framework;

namespace Tests.Workflows.Publish;

public sealed class CssSerializerMinifierUnit : ITestCase
{
    public string Name => "Serializer removes trailing semicolons and minifies numbers";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext _)
    {
        string css = ".x { color: red; } .y { margin: 0px 0rem .50em 1.0px; }";

        // Tokenize → Minify tokens → Serialize
        CssTokenizer tokenizer = new(css);
        System.Collections.Generic.List<CssToken> tokens = tokenizer.Tokenize(preserveLicenseComments: true);
        System.Collections.Generic.List<CssToken> minified = CssTokenMinifier.Minify(tokens);
        string output = CssSerializer.Serialize(minified);

        // Expect minimal form: trailing ; removed before }, zero-units stripped, decimals normalized
        Assert.AreEqual(".x{color:red}.y{margin:0 0 .5em 1px}", output, "Unexpected serialized/minified CSS");
    }
}
