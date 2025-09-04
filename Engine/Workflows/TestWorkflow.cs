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
        foreach (TestResult r in result.Results)
        {
            if (r.Passed)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("PASS ");
                Console.ResetColor();
                Console.WriteLine($"{r.Name}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("FAIL ");
                Console.ResetColor();
                Console.WriteLine($"{r.Name}");
                if (!string.IsNullOrWhiteSpace(r.Message))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  {r.File}");
                    Console.ResetColor();
                    Console.WriteLine($"  {r.Message}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Passed: {result.Passed}, Failed: {result.Failed}, Total: {result.Total} in {result.DurationMs}ms");
    }
}
