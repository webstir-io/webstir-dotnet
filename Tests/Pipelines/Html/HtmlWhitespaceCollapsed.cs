using System;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlWhitespaceCollapsed : ITestCase
{
    public string Name => "HTML publish minifies output and preserves inline script content";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        string distHtml = homePage.Html;

        Assert.IsFalse(distHtml.Contains("\n", StringComparison.Ordinal), "Published HTML should not contain newlines after minification");
        Assert.IsFalse(distHtml.Contains("\t", StringComparison.Ordinal), "Published HTML should not contain tabs after minification");
        Assert.Contains("<style data-critical=\"\">", distHtml, "Critical CSS inline style should be present");
        Assert.Contains("<script type=\"module\" src=\"/pages/home/index", distHtml, "Publish should rewrite module script path");
        string collapsed = homePage.HtmlNormalized
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal);
        Assert.Contains("</main></body></html>", collapsed, "Closing tags should remain intact after minification");
    }
}
