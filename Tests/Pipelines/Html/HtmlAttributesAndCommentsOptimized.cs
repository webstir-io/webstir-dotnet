using System;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlAttributesAndCommentsOptimized : ITestCase
{
    public string Name => "HTML publish preserves body comments and attribute semantics";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(context);
        string distHtml = scenario.GetPage(Folders.Home).Html;

        Assert.DoesNotContain("<!-- head comment should be removed -->", distHtml, "Head comment should be stripped");
        Assert.Contains("<!-- body comment should be removed -->", distHtml, "Body comment should remain in published HTML");

        Assert.Contains("<button disabled=\"disabled\" class=\"primary\"", distHtml, "Boolean and class attributes should remain quoted");
        Assert.Contains("data-info=\"foo bar\"", distHtml, "Data attribute with spaces should stay quoted");
        Assert.Contains("rel=\"nofollow\"", distHtml, "rel attribute should remain quoted");
    }
}
