using Tests.Framework;

namespace Tests.Workflows.Publish;

public sealed class PublishTests : TestSuite
{
    public override string Name => "Publish Tests";

    public override Task<TestResult[]> RunAsync()
    {
        TestCaseContext context = BuildContext();
        List<TestResult> results = [];

        ITestCase[] cases =
        [
            new PublishRunsWithoutErrors(),
            new ClientArtifactsExist(),
            new JsIsMinified(),
            new CssIsMinified(),
            new ManifestIntegrity(),
            new HtmlWhitespaceCollapsed()
        ];

        foreach (ITestCase testCase in FilterByMode(cases))
        {
            results.Add(RunTest(testCase.Name, () => testCase.Execute(context)));
        }

        return Task.FromResult(results.ToArray());
    }

    private static IEnumerable<ITestCase> FilterByMode(IEnumerable<ITestCase> cases)
    {
        bool runFull = TestMode.IsFull;
        return runFull ? cases : cases.Where(c => c.Category == TestCategory.Quick);
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
