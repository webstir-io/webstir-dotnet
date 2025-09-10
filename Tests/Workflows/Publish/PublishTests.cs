using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            new HtmlWhitespaceCollapsed(),
            // CSS minifier/prefixer invariants (quick)
            new CssMinifierInvariants(),
            new CssLicenseCommentsPreserved(),
            new CssCalcSpacingValid(),
            new CssModernPrefixesOnly(),
            // Tokenizer/serializer unit tests (full only)
            new CssTokenizerUnit(),
            new CssSerializerMinifierUnit()
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
