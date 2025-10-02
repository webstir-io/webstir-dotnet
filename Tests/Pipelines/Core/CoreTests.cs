using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tests.Framework;

namespace Tests.Pipelines.Core;

public sealed class CoreTests : TestSuite
{
    public override string Name => "Core Pipeline Tests";

    public override async Task<TestResult[]> RunAsync()
    {
        TestCaseContext context = BuildContext();

        ITestCase[] cases =
        [
            new ManifestIntegrity(),
            new PrecompressedArtifactsExist(),
            new RobotsTxtExists()
        ];

        ITestCase[] selected = FilterByMode(cases).ToArray();
        IEnumerable<(string TestName, Func<Task> TestAction)> tests = selected
            .Select(testCase => (testCase.Name, (Func<Task>)(() => Task.Run(() => testCase.Execute(context)))));

        return await RunTestsAsync(tests, runInParallel: true);
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
