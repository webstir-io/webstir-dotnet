using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Publish;

public sealed class PublishRunsWithoutErrors : ITestCase
{
    public string Name => "Publish command runs without compilation errors";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Reuse the default publish scenario to assert the publish succeeded without
        // doing another CLI publish.
        Tests.Pipelines.Html.HtmlPublishScenarioResult scenario = Tests.Pipelines.Html.HtmlPublishScenarios.HeadCombined(context);
        ProcessRunner.ProcessResult result = scenario.PublishResult;
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Publish} command failed. Error: {result.Error}");
        context.AssertNoCompilationErrors(result);
    }
}
