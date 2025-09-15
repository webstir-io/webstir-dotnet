using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tests.Framework;

namespace Tests.Pipelines.Css;

public sealed class CssTests : TestSuite
{
    public override string Name => "CSS Pipeline Tests";

    public override Task<TestResult[]> RunAsync()
    {
        TestCaseContext context = BuildContext();
        List<TestResult> results = [];

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
