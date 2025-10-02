using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tests.Framework;

namespace Tests.Pipelines.JavaScript;

public sealed class JavaScriptTests : TestSuite
{
    public override string Name => "JavaScript Pipeline Tests";

    public override async Task<TestResult[]> RunAsync()
    {
        TestCaseContext context = BuildContext();

        ITestCase[] cases =
        [
            new JsIsMinified(),
            // Precompression existence checks consolidated under Core tests
            new JsTreeShakingRemovesUnusedExports()
        ];

        ITestCase[] selected = FilterByMode(cases).ToArray();
        IEnumerable<(string TestName, Func<Task> TestAction)> tests = selected
            .Select(testCase => (testCase.Name, (Func<Task>)(() => Task.Run(() => testCase.Execute(context)))));

        int dop = Math.Max(1, Math.Min(Environment.ProcessorCount, 2));
        return await RunTestsAsync(tests, runInParallel: true, maxDegreeOfParallelism: dop);
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
