# Test Suites and Test Cases

This architecture introduces a minimal contract for individual test cases and a thin suite file per workflow that composes them, preserving the existing runner and DI model.

## Contracts

```csharp
// Tests/Framework/ITestCase.cs
namespace Tests.Framework;

public interface ITestCase
{
    string Name { get; }
    TestCategory Category { get; } // Quick or Full
    Task ExecuteAsync(TestCaseContext ctx);
}

// Tests/Framework/TestCategory.cs
namespace Tests.Framework;

public enum TestCategory
{
    Quick = 0,
    Full = 1
}

// Tests/Framework/TestCaseContext.cs
namespace Tests.Framework;

public sealed class TestCaseContext
{
    public required Cli Cli { get; init; }            // wrapper to run CLI commands
    public required SeedProjectFixture Seed { get; init; }
    public required string OutPath { get; init; }

    public void AssertNoCompilationErrors(ProcessRunner.ProcessResult result)
    {
        Assert.DoesNotContain("error CS", result.Output, "Has C# compilation errors");
        Assert.DoesNotContain("error TS", result.Output, "Has TypeScript compilation errors");
    }
}

// Tests/Framework/TestCaseBase.cs (optional convenience)
namespace Tests.Framework;

public abstract class TestCaseBase : ITestCase
{
    public abstract string Name { get; }
    public virtual TestCategory Category => TestCategory.Quick;
    public abstract Task ExecuteAsync(TestCaseContext ctx);
}
```

## Suite Pattern

Each workflow defines a single `*Tests` class (e.g., `PublishTests`) that implements `ITestSuite` and runs its `ITestCase` instances. This keeps discovery simple and the existing `Program.cs` unchanged aside from DI registrations.

```csharp
// Tests/Workflows/Publish/PublishTests.cs
using Tests.Framework;

namespace Tests.Workflows.Publish;

public sealed class PublishTests : TestSuite
{
    public override string Name => "Publish Tests";

    public override Task<TestResult[]> RunAsync()
    {
        TestCaseContext ctx = BuildContext();
        List<TestResult> tests = [];

        ITestCase[] cases =
        [
            new PublishRunsWithoutErrors(),
            new ClientArtifactsExist(),
            new JsIsMinified(),
            new CssIsMinified(),
            new HtmlWhitespaceCollapsed(),
            new ManifestIntegrity()
        ];

        foreach (ITestCase c in FilterByMode(cases))
        {
            tests.Add(RunTest(c.Name, () => c.ExecuteAsync(ctx)));
        }

        return Task.FromResult(tests.ToArray());
    }

    private static IEnumerable<ITestCase> FilterByMode(IEnumerable<ITestCase> cases)
    {
        bool runFull = Tests.Framework.TestMode.IsFull;
        return runFull ? cases : cases.Where(c => c.Category == TestCategory.Quick);
    }

    private static TestCaseContext BuildContext()
    {
        return new TestCaseContext
        {
            Cli = new Cli(),
            Seed = new SeedProjectFixture(),
            OutPath = Paths.OutPath
        };
    }
}
```

## DI Registration (Tests/Program.cs)

```csharp
// Register workflow suites as ITestSuite
services.AddTransient<ITestSuite, Tests.Workflows.Init.InitTests>();
services.AddTransient<ITestSuite, Tests.Workflows.Build.BuildTests>();
services.AddTransient<ITestSuite, Tests.Workflows.Publish.PublishTests>();
services.AddTransient<ITestSuite, Tests.Workflows.Watch.WatchTests>();
services.AddTransient<ITestSuite, Tests.Workflows.Help.HelpTests>();
```

Notes:
- Discovery can remain explicit (as above) or evolve to simple reflection later.
- `TestCaseBase` is optional; explicit `ITestCase` implementations are perfectly fine.
