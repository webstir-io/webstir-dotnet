using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Engine.Pipelines.Core.Testing;
using Engine.Workers;

namespace Engine.Workflows;

public sealed class TestWorkflow(
    AppWorkspace context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker) : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
{

    public override string WorkflowName => Commands.Test;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        await ExecuteBuildAsync();

        IEnumerable<string> source = TestDiscovery.FindSourceTests(Context);
        if (!source.Any())
        {
            Console.WriteLine("No tests found under src/**/tests/");
            return;
        }

        IEnumerable<string> compiled = TestDiscovery.MapToCompiled(source, Context);
        RunResult result = await TestRunner.RunAsync(compiled, CancellationToken.None);
        PrintResults(result);

        // Set a non-zero exit code when failures occur
        if (result.Failed > 0)
        {
            Environment.ExitCode = 1;
        }
    }

    private static void PrintResults(RunResult result)
    {
        bool anyFailures = false;
        foreach (TestResult testResult in result.Results)
        {
            if (testResult.Passed)
            {
                continue;
            }

            anyFailures = true;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("FAIL ");
            Console.ResetColor();
            Console.WriteLine($"{testResult.Name}");
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
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("✔ ");
            Console.ResetColor();
            Console.WriteLine("All tests passed");
        }
    }
}
