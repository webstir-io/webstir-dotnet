using System;
using System.IO;
using Engine;
using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlPublishOmitsRuntimeScripts : ITestCase
{
    public string Name => "Publish output omits development runtime scripts";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(context);
        context.AssertNoCompilationErrors(scenario.PublishResult);
        Assert.AreEqual(0, scenario.PublishResult.ExitCode, $"{Commands.Publish} command failed. Error: {scenario.PublishResult.Error}");

        string distFrontend = scenario.DistFrontendPath;
        string refreshRuntimePath = Path.Combine(distFrontend, Files.RefreshJs);
        string hmrRuntimePath = Path.Combine(distFrontend, Files.HmrJs);

        Assert.IsFalse(File.Exists(refreshRuntimePath), $"{Files.RefreshJs} should not be emitted in publish output");
        Assert.IsFalse(File.Exists(hmrRuntimePath), $"{Files.HmrJs} should not be emitted in publish output");

        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        string distHtml = homePage.HtmlNormalized;
        Assert.DoesNotContain("/refresh.js", distHtml, "Published HTML should not reference refresh runtime script");
        Assert.DoesNotContain("/hmr.js", distHtml, "Published HTML should not reference HMR runtime script");
    }

}
