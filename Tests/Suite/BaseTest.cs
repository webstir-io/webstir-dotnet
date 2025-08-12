using System.Diagnostics;
using Tests.Framework;
using Engine;

namespace Tests.Suite;

public abstract class BaseTest : TestSuite
{
    protected string CliBinaryPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLI.dll");
    
    protected ProcessRunner.ProcessResult RunCliCommand(
        string arguments, 
        string? workingDirectory = null, 
        int timeoutMs = 10000,
        string? waitForSignal = null)
    {
        return ProcessRunner.Run(new ProcessRunOptions
        {
            FileName = "dotnet",
            Arguments = $"\"{CliBinaryPath}\" {arguments}",
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            ExitTimeoutMs = timeoutMs,
            WaitForSignal = waitForSignal,
            WaitForSignalTimeoutMs = waitForSignal != null ? 8000 : 5000,
            TerminationMethod = waitForSignal != null ? TerminationMethod.CtrlC : TerminationMethod.Kill
        });
    }
    
    protected void SetupProject(string projectDir)
    {
        CleanupDirectory(projectDir);
        Directory.CreateDirectory(projectDir);
        
        // Use init command to create a proper project
        var result = RunCliCommand(Commands.Init, projectDir, timeoutMs: 10000);
        
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to setup project with {Commands.Init} command. Error: {result.Error}");
    }
    
    protected void CleanupDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }
    
    protected void AssertNoCompilationErrors(ProcessRunner.ProcessResult result)
    {
        Assert.DoesNotContain("error CS", result.Output, "Has C# compilation errors");
        Assert.DoesNotContain("error TS", result.Output, "Has TypeScript compilation errors");
    }
}

