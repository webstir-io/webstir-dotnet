# Test Conventions

## Naming
- One class per test case; one file per class.
- Class name matches file (e.g., `PublishRunsWithoutErrors.cs`).
- `Name` is user-facing, concise, and descriptive.

## Categories
- Default to `Quick` unless the test is slow, integration-heavy, or long-running.
- Mark watch-mode and deep publish validations as `Full`.

## Helpers and Paths
- Use `Folders`, `Files`, and `FileExtensions` constants (no magic strings for paths).
- Use `AppWorkspace` patterns indirectly via fixtures and helpers.
- Prefer small workflow-local helpers; promote to `Framework` only when clearly reusable.

## Timeouts and Signals
- Keep timeouts tight but realistic; prefer ~10–15s per external run.
- For watch-mode, use `waitForSignal` to gate readiness, then stop via Ctrl+C.

## Style
- Follow `.codex/style.md` and `.editorconfig`.
- Keep methods focused; prefer small helpers over long inline blocks.
- Avoid single-letter identifiers.
- Limit lines to ~120 characters.

## Example (Case Skeleton)

```csharp
namespace Tests.Workflows.Publish.Tests;

using Tests.Framework;

public sealed class PublishRunsWithoutErrors : TestCaseBase
{
    public override string Name => "Publish command runs without compilation errors";

    public override async Task ExecuteAsync(TestCaseContext ctx)
    {
        ctx.Seed.Ensure();
        ctx.Seed.CleanBuildAndDist();

        var result = ctx.Cli.Run($"{Engine.Commands.Publish} {Engine.ProjectOptions.ProjectName} seed", ctx.OutPath, timeoutMs: 15000);

        Assert.AreEqual(0, result.ExitCode, $"publish failed: {result.Error}");
        ctx.AssertNoCompilationErrors(result);
    }
}
```

## Add A New Test Case
- Pick the workflow folder: `Tests/Workflows/<Workflow>/`.
- Create a file named after the case (e.g., `CssIsMinified.cs`).
- Implement `ITestCase` with:
  - `Name`: user-facing description.
  - `Category`: `Quick` for fast checks; `Full` for longer ones.
  - `Execute(TestCaseContext ctx)`: write the assertions; use `ctx.Cli.Run(...)` and constants from `Folders/Files/FileExtensions`.
- Register it in `<Workflow>Tests.cs` by adding it to the `cases` array.
- Keep setup minimal; prefer reusing seed projects where possible.
- Run quick/full locally:
  - `dotnet run --project Tests`
  - `dotnet run --project Tests -- --full`

## Add A New Suite
- Create folder: `Tests/Workflows/<Workflow>/` if it doesn’t exist.
- Add `<Workflow>Tests.cs` implementing `ITestSuite`.
  - Build a `TestCaseContext` (`Cli`, `OutPath`).
  - Compose an array of `ITestCase` instances.
  - Filter with `TestMode.IsFull` and run via `RunTest(...)`.
- Register the suite in `Tests/Program.cs` using a `using` alias:
  - `using FooWorkflowTests = Tests.Workflows.Foo.FooTests;`
  - `services.AddTransient<ITestSuite, FooWorkflowTests>();`
- Keep suite logic minimal; put behavior in case files.
