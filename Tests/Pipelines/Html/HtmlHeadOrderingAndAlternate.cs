using System;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlHeadOrderingAndAlternate : ITestCase
{
    public string Name => "HTML head orders charset/viewport and dedupes alternate hreflang (page overrides)";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(context);
        string distHtml = scenario.GetPage(Folders.Home).Html;

        // One viewport with page content
        Assert.AreEqual(1, CountOccurrences(distHtml, "name=\"viewport\""), "Viewport should be single");
        Assert.Contains("content=\"page-viewport\"", distHtml, "Viewport value should be from page");

        // Alternate hreflang dedup: 'en' overridden by page, 'fr' retained from template
        Assert.AreEqual(1, CountOccurrences(distHtml, "rel=\"alternate\" hreflang=\"en\""), "en alternate should be single");
        Assert.Contains("href=\"/en/home-page\"", distHtml, "en alternate should come from page");
        Assert.AreEqual(1, CountOccurrences(distHtml, "rel=\"alternate\" hreflang=\"fr\""), "fr alternate should be single");
        Assert.Contains("href=\"/fr/home\"", distHtml, "fr alternate should remain from template");

        // Ordering: charset first, viewport early (before <title>)
        int headStart = distHtml.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
        int headEnd = distHtml.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(headStart >= 0 && headEnd > headStart, "Head section should exist");
        string headInner = distHtml.Substring(headStart, headEnd - headStart);
        int charsetPos = headInner.IndexOf("<meta charset=", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(charsetPos >= 0, "Charset meta should exist");
        int viewportPos = headInner.IndexOf("name=\"viewport\"", StringComparison.OrdinalIgnoreCase);
        int titlePos = headInner.IndexOf("<title>", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(viewportPos >= 0 && titlePos >= 0 && viewportPos < titlePos, "Viewport should appear before title");
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while (true)
        {
            index = text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }
            count++;
            index += value.Length;
        }
        return count;
    }
}
