using System;
using System.Threading;
using global::Utilities.ProcessRunner;
using SharedProcessRunner = global::Utilities.ProcessRunner.ProcessRunner;

namespace Tester.Infrastructure;

public static class ProcessRunner
{
    private static readonly IProcessRunner Runner = new SharedProcessRunner();

    public sealed class ProcessResult
    {
        public int ExitCode
        {
            get; init;
        }
        public string Output { get; init; } = string.Empty;
        public string Error { get; init; } = string.Empty;
        public bool TimedOut
        {
            get; init;
        }
        public bool ReceivedReadySignal
        {
            get; set;
        }
    }

    public static ProcessResult Run(ProcessRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ProcessSpec spec = new()
        {
            FileName = options.FileName,
            Arguments = options.Arguments,
            WorkingDirectory = options.WorkingDirectory,
            ExitTimeout = options.ExitTimeoutMs > 0 ? TimeSpan.FromMilliseconds(options.ExitTimeoutMs) : null,
            TerminationMethod = options.TerminationMethod,
            RedirectStandardInput = false,
            WaitForReadySignalOnStart = false,
            ReadySignal = options.WaitForSignal,
            ReadySignalTimeout = TimeSpan.FromMilliseconds(options.WaitForSignalTimeoutMs)
        };

        ProcessResult Map(global::Utilities.ProcessRunner.ProcessResult source) => new ProcessResult
        {
            ExitCode = source.ExitCode,
            Output = source.StandardOutput,
            Error = source.StandardError,
            TimedOut = source.TimedOut,
            ReceivedReadySignal = source.ReadySignalReceived
        };

        if (options.WaitForSignal is null)
        {
            global::Utilities.ProcessRunner.ProcessResult result = Runner.RunAsync(spec, CancellationToken.None).GetAwaiter().GetResult();
            return Map(result);
        }

        IProcessHandle handle = Runner.StartAsync(spec, CancellationToken.None).GetAwaiter().GetResult();
        try
        {
            bool ready = handle.WaitForReadyAsync(TimeSpan.FromMilliseconds(options.WaitForSignalTimeoutMs), CancellationToken.None).GetAwaiter().GetResult();
            global::Utilities.ProcessRunner.ProcessResult stopResult = handle.StopAsync(options.TerminationMethod, CancellationToken.None).GetAwaiter().GetResult();
            ProcessResult mapped = Map(stopResult);
            mapped.ReceivedReadySignal = ready || stopResult.ReadySignalReceived;
            return mapped;
        }
        finally
        {
            handle.DisposeAsync().GetAwaiter().GetResult();
        }
    }
}

public sealed class ProcessRunOptions
{
    public required string FileName
    {
        get; init;
    }
    public required string Arguments
    {
        get; init;
    }
    public required string WorkingDirectory
    {
        get; init;
    }
    public int ExitTimeoutMs { get; init; } = 10000;
    public string? WaitForSignal
    {
        get; init;
    }
    public int WaitForSignalTimeoutMs { get; init; } = 5000;
    public TerminationMethod TerminationMethod { get; init; } = TerminationMethod.Kill;
}
