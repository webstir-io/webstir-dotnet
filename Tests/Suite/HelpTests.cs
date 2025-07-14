using Tests.Framework;
using Engine;

namespace Tests.Suite;

public class HelpTests : BaseTest
{
    public override string Name => "Help Tests";
    
    public override Task<TestResult[]> RunAsync()
    {
        TestResult[] tests = [
            RunTest($"{App.Options.Help} command shows available commands", TestHelpCommandOutput)
        ];
        return Task.FromResult(tests);
    }
    
    private void TestHelpCommandOutput()
    {        
        var result = RunCliCommand(App.Options.Help, timeoutMs: 5000);
        
        if (result.TimedOut)
            Assert.Fail($"{App.Options.Help} command timed out");
        
        // Help command should run successfully (exit code 0 or 1 are both acceptable for help)
        Assert.IsTrue(result.ExitCode == 0 || result.ExitCode == 1, 
            $"{App.Options.Help} command failed with exit code {result.ExitCode}. Error: {result.Error}");
        
        // Should produce meaningful output
        Assert.GreaterThan(10, result.Output.Length, "Help output is empty");
        
        // Should mention key commands (case insensitive)
        var lowerOutput = result.Output.ToLowerInvariant();
        Assert.Contains(App.Commands.Build, lowerOutput, $"Help does not mention {App.Commands.Build} command");
        Assert.Contains(App.Commands.Demo, lowerOutput, $"Help does not mention {App.Commands.Demo} command");
    }
    
    // TODO: Add more help command tests here
    // - Test help for specific commands (build --help, demo --help)
    // - Test invalid command help
    // - Test help output formatting
}