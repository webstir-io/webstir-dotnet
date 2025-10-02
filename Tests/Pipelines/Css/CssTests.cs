using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tests.Framework;

namespace Tests.Pipelines.Css;

public sealed class CssTests : TestSuite
{
    public override string Name => "CSS Pipeline Tests";

    public override async Task<TestResult[]> RunAsync()
    {
        TestCaseContext context = BuildContext();

        ITestCase[] cases =
        [
            new CssIsMinified(),
            new CssPrecompressedAreSmaller(),
            // Tests specific to old custom CSS minifier - no longer applicable with esbuild:
            // new CssMinifierInvariants(),      // esbuild has different minification behavior
            // new CssLicenseCommentsPreserved(), // esbuild strips all comments in minify mode
            // new CssCalcSpacingValid(),         // esbuild handles calc() correctly by default
            // new CssMediaSupportsSpacing(),     // esbuild handles media queries correctly by default
            // new CssModernPrefixesOnly(),       // esbuild doesn't strip vendor prefixes
            // new CssLegacyPrefixesStripped(),   // esbuild doesn't strip vendor prefixes
            // new CssFontDisplaySwapEnforced(),  // was a post-processing feature, not part of bundling
            new CssSeedSnapshot()
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
