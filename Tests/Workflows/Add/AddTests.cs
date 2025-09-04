using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tests.Framework;

namespace Tests.Workflows.Add;

public sealed class AddTests : TestSuite
{
    public override string Name => "Add Workflow";

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
            new AddPageCreatesFiles(),
            new AddTestScaffoldsFile()
        ];

        IEnumerable<ITestCase> selected = TestMode.IsFull ? cases : cases.Where(c => c.Category == TestCategory.Quick);
        foreach (ITestCase testCase in selected)
        {
            results.Add(RunTest(testCase.Name, () => testCase.Execute(context)));
        }

        return Task.FromResult(results.ToArray());
    }
}

