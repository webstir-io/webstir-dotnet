using Tests.Framework;

namespace Tests.Workflows.Build;

public sealed class BuildTests : TestSuite
{
    public override string Name => "Build Tests";

    public override Task<TestResult[]> RunAsync()
    {
        TestCaseContext context = new()
        {
            Cli = new Cli(),
            OutPath = Paths.OutPath
        };

        List<TestResult> results = [];

        ITestCase[] cases =
        [
            new BuildRunsWithoutErrors(),
            new MissingAppHtmlShowsError()
        ];

        IEnumerable<ITestCase> selected = TestMode.IsFull ? cases : cases.Where(c => c.Category == TestCategory.Quick);
        foreach (ITestCase testCase in selected)
        {
            results.Add(RunTest(testCase.Name, () => testCase.Execute(context)));
        }

        return Task.FromResult(results.ToArray());
    }
}
