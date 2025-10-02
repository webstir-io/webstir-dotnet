using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tests.Framework;
using Tests.Pipelines.Css;
using Tests.Pipelines.JavaScript;
using Tests.Pipelines.Html;
using Tests.Pipelines.Core;

namespace Tests.Workflows.Publish;

public sealed class PublishTests : TestSuite
{
    public override string Name => "Publish Tests";

    public override async Task<TestResult[]> RunAsync()
    {
        List<TestResult> results = [];

        // Workflow-level tests
        TestCaseContext context = BuildContext();
        ITestCase[] workflowCases =
        [
            new PublishRunsWithoutErrors()
        ];

        ITestCase[] selectedWorkflows = FilterByMode(workflowCases).ToArray();
        if (selectedWorkflows.Length > 0)
        {
            TestResult[] workflowResults = await RunTestsAsync(selectedWorkflows
                .Select(testCase => (testCase.Name, (Func<Task>)(() => Task.Run(() => testCase.Execute(context))))));
            results.AddRange(workflowResults);
        }

        // Run pipeline test suites
        TestSuite[] pipelineSuites =
        [
            new CssTests(),
            new JavaScriptTests(),
            new HtmlTests(),
            new CoreTests()
        ];

        Task<TestResult[]>[] pipelineTasks = pipelineSuites
            .Select(suite => suite.RunAsync())
            .ToArray();

        TestResult[][] pipelineResults = await Task.WhenAll(pipelineTasks);
        foreach (TestResult[] suiteResults in pipelineResults)
        {
            results.AddRange(suiteResults);
        }

        return [.. results];
    }

    private static IEnumerable<ITestCase> FilterByMode(IEnumerable<ITestCase> cases)
    {
        bool runFull = TestMode.IsFull;
        return runFull ? cases : cases.Where(testCase => testCase.Category == TestCategory.Quick);
    }

    private static TestCaseContext BuildContext()
    {
        return new TestCaseContext
        {
            Cli = new Cli(),
            OutPath = Paths.OutPath
        };
    }
}
