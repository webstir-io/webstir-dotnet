using System;
using Engine;
using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlPublishIncludesMetaDescription : ITestCase
{
    public string Name => "Publish output includes meta description";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.Default(context);
        context.AssertNoCompilationErrors(scenario.PublishResult);
        Assert.AreEqual(0, scenario.PublishResult.ExitCode, $"{Commands.Publish} command failed. Error: {scenario.PublishResult.Error}");

        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        string distHtml = homePage.HtmlNormalized;

        Assert.Contains("meta name=\"description\"", distHtml, "Published HTML should include a meta description.");
        Assert.Contains("Starter description for your Webstir app.", distHtml, "Default meta description missing from published HTML.");
    }
}
