using System;
using System.IO;

namespace Tester.Infrastructure;

public sealed class Runner
{
    private static string CliBinaryPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLI.dll");

    public ProcessRunner.ProcessResult Run(
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
            WaitForSignalTimeoutMs = waitForSignal is not null ? 15000 : 5000,
            TerminationMethod = waitForSignal is not null ? TerminationMethod.CtrlC : TerminationMethod.Kill
        });
    }
}
