using System;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Help;

public sealed class HelpShowsKeyCommands : ITestCase
{
    public string Name => "Help command shows available commands";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ProcessRunner.ProcessResult result = context.Cli.Run(HelpOptions.Help, timeoutMs: 5000);

        if (result.TimedOut)
        {
            Assert.Fail($"{HelpOptions.Help} command timed out");
        }

        Assert.IsTrue(result.ExitCode is 0 or 1, $"{HelpOptions.Help} failed with exit code {result.ExitCode}. Error: {result.Error}");

        Assert.GreaterThan(10, result.Output.Length, "Help output is empty");

        string lower = result.Output.ToLowerInvariant();
        Assert.Contains(Commands.Build, lower, $"Help does not mention {Commands.Build} command");
        // Demo command intentionally removed for now
    }
}
