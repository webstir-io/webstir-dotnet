namespace Tests.Framework;

public sealed class TestCaseContext
{
    public required Cli Cli { get; init; }
    public required string OutPath { get; init; }

    public void AssertNoCompilationErrors(ProcessRunner.ProcessResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Assert.DoesNotContain("error CS", result.Output, "Has C# compilation errors");
        Assert.DoesNotContain("error TS", result.Output, "Has TypeScript compilation errors");
    }
}
