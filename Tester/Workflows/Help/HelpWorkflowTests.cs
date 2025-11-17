using System;
using Engine;
using Tester.Infrastructure;
using Utilities.Process;
using Xunit;

namespace Tester.Workflows.Help;

public sealed class HelpWorkflowTests
{
    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void HelpShowsKeyCommands()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = new(new Runner(), Paths.OutPath);
        ProcessResult result = context.Run(HelpOptions.Help, timeoutMs: 5000);

        Assert.True(result.ExitCode is 0 or 1, $"help failed with exit code {result.ExitCode}. Error: {result.StandardError}");

        string combinedOutput = string.Concat(result.StandardOutput ?? string.Empty, result.StandardError ?? string.Empty);
        Assert.True(combinedOutput.Length > 10, "Help output is empty");
        Assert.Contains("Usage:", combinedOutput, StringComparison.Ordinal);
        Assert.Contains("Commands:", combinedOutput, StringComparison.Ordinal);
    }
}
