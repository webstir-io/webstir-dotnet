using System;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlMetaPreservation : ITestCase
{
    public string Name => "HTML head preserves and dedupes meta/link canonical; page overrides template";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(context);
        string distHtml = scenario.GetPage(Folders.Home).Html;

        // Assertions (accounting for minified HTML without quotes around attributes)
        int viewportCount = CountOccurrences(distHtml, "name=\"viewport\"");
        Assert.AreEqual(1, viewportCount, "Viewport meta should appear exactly once");
        Assert.Contains("content=\"page-viewport\"", distHtml, "Viewport meta should use page value");

        int canonicalCount = CountOccurrences(distHtml, "rel=\"canonical\"");
        Assert.AreEqual(1, canonicalCount, "Canonical link should appear exactly once");
        Assert.Contains("href=\"/home\"", distHtml, "Canonical href should come from page");

        Assert.Contains("name=\"description\"", distHtml, "Description meta should be preserved");
        Assert.Contains("content=\"page-desc\"", distHtml, "Description content should be preserved");
        Assert.Contains("property=\"og:title\"", distHtml, "OG title should be preserved");
        Assert.Contains("content=\"OG Page Title\"", distHtml, "OG title content should be preserved");
        Assert.Contains("data-test=\"head-script\"", distHtml, "Head script from page should be preserved");
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
