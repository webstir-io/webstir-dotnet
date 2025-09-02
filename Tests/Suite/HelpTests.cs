using Tests.Framework;
using Engine;

namespace Tests.Suite;

public class HelpTests : BaseTest
{
    public override string Name => "Help Tests";
    
    public override Task<TestResult[]> RunAsync()
    {
        if (!Tests.Framework.TestMode.IsFull)
        {
            return Task.FromResult(Array.Empty<TestResult>());
        }
        TestResult[] tests = [
            RunTest($"{HelpOptions.Help} command shows available commands", TestHelpCommandOutput)
        ];
        return Task.FromResult(tests);
    }
    
    private void TestHelpCommandOutput()
    {        
        ProcessRunner.ProcessResult result = RunCliCommand(HelpOptions.Help, timeoutMs: 5000);

        if (result.TimedOut)
            Assert.Fail($"{HelpOptions.Help} command timed out");

        // Help command should run successfully (exit code 0 or 1 are both acceptable for help)
        Assert.IsTrue(result.ExitCode is 0 or 1, 
            $"{HelpOptions.Help} command failed with exit code {result.ExitCode}. Error: {result.Error}");

        // Should produce meaningful output
        Assert.GreaterThan(10, result.Output.Length, "Help output is empty");
        
        // Should mention key commands (case insensitive)
        string lowerOutput = result.Output.ToLowerInvariant();
        Assert.Contains(Commands.Build, lowerOutput, $"Help does not mention {Commands.Build} command");
        Assert.Contains(Commands.Demo, lowerOutput, $"Help does not mention {Commands.Demo} command");
    }
    
    // TODO: Add more help command tests here
    // - Test help for specific commands (build --help, demo --help)
    // - Test invalid command help
    // - Test help output formatting
}
