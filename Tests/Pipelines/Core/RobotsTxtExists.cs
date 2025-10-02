using System;
using System.IO;
using Engine;
using Tests.Framework;

namespace Tests.Pipelines.Core;

public sealed class RobotsTxtExists : ITestCase
{
    public string Name => "robots.txt exists and allows all";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Tests.Pipelines.Html.HtmlPublishScenarioResult scenario = Tests.Pipelines.Html.HtmlPublishScenarios.HeadCombined(context);
        string robotsPath = Path.Combine(scenario.DistFrontendPath, Files.RobotsTxt);
        Assert.IsTrue(File.Exists(robotsPath), "robots.txt missing in dist/frontend");
        string text = File.ReadAllText(robotsPath);
        Assert.Contains("User-agent: *", text, "Expected allow-all robots.txt");
    }
}
