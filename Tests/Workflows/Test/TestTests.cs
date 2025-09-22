using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tests.Framework;

namespace Tests.Workflows.Test;

public sealed class TestTests : TestSuite
{
    public override string Name => "Test Workflow";

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
            new BackendTestsExecute()
        ];

        IEnumerable<ITestCase> selected = TestMode.IsFull ? cases : cases.Where(c => c.Category == TestCategory.Quick);
        foreach (ITestCase testCase in selected)
        {
            results.Add(RunTest(testCase.Name, () => testCase.Execute(context)));
        }

        return Task.FromResult(results.ToArray());
    }
}
