using System;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlWhitespaceCollapsed : ITestCase
{
    public string Name => "HTML publish keeps readable formatting and preserves inline script content";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        string distHtml = homePage.Html;

        Assert.Contains("<html lang=\"en\"><head>\n", distHtml, "Document should start with readable head block");
        Assert.Contains("\n<body>\n    <main>", distHtml, "Body/main blocks should be expanded on separate lines");
        Assert.Contains("<style data-critical=\"\">", distHtml, "Critical CSS inline style should be present");
        Assert.Contains("<script type=\"module\" src=\"/pages/home/index", distHtml, "Publish should rewrite module script path");
        string collapsed = homePage.HtmlNormalized
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal);
        Assert.Contains("</main></body></html>", collapsed, "Closing tags should retain readable formatting");
    }
}
