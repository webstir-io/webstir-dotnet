using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tests.Framework;

namespace Tests.Pipelines.JavaScript;

public sealed class JavaScriptTests : TestSuite
{
    public override string Name => "JavaScript Pipeline Tests";

    public override Task<TestResult[]> RunAsync()
    {
        TestCaseContext context = BuildContext();
        List<TestResult> results = [];

        ITestCase[] cases =
        [
            new JsIsMinified(),
            new JsMinifierInvariants(),
            new JsPrecompressedArtifactsExist()
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