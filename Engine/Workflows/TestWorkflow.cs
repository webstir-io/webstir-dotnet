using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Engine.Bridge.Test;
using Engine.Helpers;
using Engine.Interfaces;

namespace Engine.Workflows;

public sealed class TestWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers) : BaseWorkflow(context, workers)
{
    public override string WorkflowName => Commands.Test;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        await ExecuteBuildAsync();

        PackageEnsureResult ensureResult = await TestPackageUtilities.EnsurePackageAsync(Context);
        TestPackageUtilities.LogEnsureMessages(ensureResult);

        TestCliRunner runner = new(Context);
        TestCliRunResult runResult = await runner.RunTestsAsync(CancellationToken.None);

        if (!runResult.TestsDiscovered)
        {
            Console.WriteLine("No tests found under src/**/tests/");
            return;
        }

        PrintResults(runResult);

        if (runResult.Failed > 0 || runResult.HadErrors || runResult.ExitCode != 0)
        {
            Environment.ExitCode = 1;
        }
    }

    private static void PrintResults(TestCliRunResult result)
    {
        bool anyFailures = false;
        foreach (TestCliTestResult testResult in result.Results)
        {
            if (testResult.Passed)
            {
                continue;
            }

            anyFailures = true;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("FAIL ");
            Console.ResetColor();
            Console.WriteLine(testResult.Name);
            if (!string.IsNullOrWhiteSpace(testResult.Message))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  {testResult.File}");
                Console.ResetColor();
                Console.WriteLine($"  {testResult.Message}");
            }
        }

        if (anyFailures)
        {
            Console.WriteLine();
            Console.WriteLine($"Passed: {result.Passed}, Failed: {result.Failed}, Total: {result.Total} in {result.DurationMs}ms");
            return;
        }

        if (result.Total > 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("✔ ");
            Console.ResetColor();
            Console.WriteLine("All tests passed");
        }
    }
}
