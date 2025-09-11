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
            new PublishRunsWithoutErrors(),
            new ClientArtifactsExist()
        ];

        foreach (ITestCase testCase in FilterByMode(workflowCases))
        {
            results.Add(RunTest(testCase.Name, () => testCase.Execute(context)));
        }

        // Run pipeline test suites
        TestSuite[] pipelineSuites =
        [
            new CssTests(),
            new JavaScriptTests(),
            new HtmlTests(),
            new CoreTests()
        ];

        foreach (TestSuite suite in pipelineSuites)
        {
            TestResult[] suiteResults = await suite.RunAsync();
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
