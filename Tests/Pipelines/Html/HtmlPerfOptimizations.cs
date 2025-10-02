using System;
using Engine;
using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlPerfOptimizations : ITestCase
{
    public string Name => "HTML publishes critical CSS, hints, and image enhancements";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.PerfPage(context);
        Assert.AreEqual(0, scenario.PublishResult.ExitCode, $"{Commands.Publish} failed: {scenario.PublishResult.Error}");

        string distHtml = scenario.GetPage("perf").HtmlNormalized;

        // Critical CSS inlined in head (formatter may add ="" to attribute)
        Assert.Contains("data-critical", distHtml, "Expected critical CSS inlined in <head>");

        // When CSS is inlined, no stylesheet link or preload should remain
        Assert.DoesNotContain("rel=\"stylesheet\"", distHtml, "Stylesheet link should be removed when CSS is inlined");
        Assert.DoesNotContain("rel=\"preload\" as=\"style\"", distHtml, "CSS preload should be removed when CSS is inlined");

        // No modulepreload since JS is loaded directly via script tag
        Assert.DoesNotContain("modulepreload", distHtml, "Modulepreload should not be present for directly loaded scripts");

        // JS should be loaded via script tag
        Assert.Contains("<script type=\"module\"", distHtml, "Expected JS to be loaded via script tag");

        // Prefetch hints present (due to anchor to /home)
        Assert.Contains("rel=\"prefetch\"", distHtml, "Expected prefetch hint for next navigation");

        // Image width/height injected and lazy-loading applied on non-first image
        Assert.Contains("width=\"1\" height=\"1\"", distHtml, "Expected width/height on images");
        Assert.Contains("loading=\"lazy\"", distHtml, "Expected lazy loading on below-the-fold images");
    }
}
