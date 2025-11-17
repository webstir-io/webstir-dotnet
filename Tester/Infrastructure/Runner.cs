using System;
using System.IO;
using System.Threading;
using Utilities.Process;

namespace Tester.Infrastructure;

public sealed class Runner
{
    private static string CliBinaryPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLI.dll");

    public ProcessResult Run(
        string arguments,
        string? workingDirectory = null,
        int timeoutMs = 10000,
        string? waitForSignal = null)
    {
        ProcessRunner runner = new();

        ProcessSpec spec = new()
        {
            FileName = "dotnet",
            Arguments = $"\"{CliBinaryPath}\" {arguments}",
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            ExitTimeout = timeoutMs > 0 ? TimeSpan.FromMilliseconds(timeoutMs) : null,
            TerminationMethod = waitForSignal is not null ? TerminationMethod.CtrlC : TerminationMethod.Kill,
            RedirectStandardInput = false,
            WaitForReadySignalOnStart = false,
            ReadySignal = waitForSignal,
            ReadySignalTimeout = TimeSpan.FromMilliseconds(waitForSignal is not null ? 15000 : 5000)
        };

        if (waitForSignal is null)
        {
            return runner.RunAsync(spec, CancellationToken.None).GetAwaiter().GetResult();
        }

        IProcessHandle handle = runner.StartAsync(spec, CancellationToken.None).GetAwaiter().GetResult();
        try
        {
            bool ready = handle
                .WaitForReadyAsync(TimeSpan.FromMilliseconds(15000), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            ProcessResult stopResult = handle
                .StopAsync(spec.TerminationMethod, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (ready && !stopResult.ReadySignalReceived)
            {
                // Preserve the ready signal flag semantics from the previous wrapper.
                return new ProcessResult
                {
                    ExitCode = stopResult.ExitCode,
                    StandardOutput = stopResult.StandardOutput,
                    StandardError = stopResult.StandardError,
                    Duration = stopResult.Duration,
                    TimedOut = stopResult.TimedOut,
                    WasCancelled = stopResult.WasCancelled,
                    IsExitCodeAccepted = stopResult.IsExitCodeAccepted,
                    ReadySignalReceived = true,
                    ReadySignalTimedOut = stopResult.ReadySignalTimedOut
                };
            }

            return stopResult;
        }
        finally
        {
            handle.DisposeAsync().GetAwaiter().GetResult();
        }
    }
}
