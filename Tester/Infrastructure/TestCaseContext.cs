using System;
using Xunit;

namespace Tester.Infrastructure;

public sealed class TestCaseContext
{
    public TestCaseContext(Runner runner, string outPath)
    {
        Runner = runner ?? throw new ArgumentNullException(nameof(runner));
        OutPath = outPath ?? throw new ArgumentNullException(nameof(outPath));
    }

    public Runner Runner
    {
        get;
    }

    public string OutPath
    {
        get;
    }

    public ProcessRunner.ProcessResult Run(string arguments, string? workingDirectory = null, int timeoutMs = 10000, string? waitForSignal = null) =>
        Runner.Run(arguments, workingDirectory, timeoutMs, waitForSignal);

    public void AssertNoCompilationErrors(ProcessRunner.ProcessResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Assert.DoesNotContain("error CS", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("error TS", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
