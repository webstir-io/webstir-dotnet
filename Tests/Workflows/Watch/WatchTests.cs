using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tests.Framework;

namespace Tests.Workflows.Watch;

public sealed class WatchTests : TestSuite
{
    public override string Name => "Watch Tests";

    public override async Task<TestResult[]> RunAsync()
    {
        TestCaseContext context = new()
        {
            Cli = new Cli(),
            OutPath = Paths.OutPath
        };

        ITestCase[] cases =
        [
            new WatchStartsAndSignalsReady()
        ];

        IEnumerable<ITestCase> selected = TestMode.IsFull ? cases : cases.Where(c => c.Category == TestCategory.Quick);
        IEnumerable<(string TestName, Func<Task> TestAction)> tests = selected
            .Select(testCase => (testCase.Name, (Func<Task>)(() => Task.Run(() => testCase.Execute(context)))));

        return await RunTestsAsync(tests, runInParallel: true);
    }
}
