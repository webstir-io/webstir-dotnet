using System;
using Engine.Pipelines.JavaScript.Minification;
using Tests.Framework;

namespace Tests.Pipelines.JavaScript;

public sealed class JsCompactModeSpacing : ITestCase
{
    public string Name => "JS compact mode preserves token boundaries (ASI/await/templates/regex)";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        string input = @"function a(x){ return x }

async function c(){
  await a(1)
  for await (const v of [1]) { a(v) }
}

function d(){
  const t = `hi ${a(1)} ok`;
  if (true) return /re\//.test('re/');
}

let i=0; i++/2; // postfix divide

function e(){ return(a) + (typeof x) }
";

        string min = JsMinifier.Minify(input, compact: true);

        // No newlines in compact output
        Assert.DoesNotContain("\n", min, "Compact output should not contain newlines");
        // typeof spacing
        Assert.DoesNotContain("typeofx", min, "typeof must have boundary");
        // await spacing
        Assert.DoesNotContain("awaita", min, "await must have boundary");
        // template literal intact (structure present)
        Assert.Contains("`hi ${", min, "template literal expression should be preserved");
        // regex literal preserved
        Assert.Contains("/re\\/", min, "regex literal should be preserved");
        // postfix divide intact
        Assert.Contains("i++/2", min, "postfix divide should be intact");
    }
}

