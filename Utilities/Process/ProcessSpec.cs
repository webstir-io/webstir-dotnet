using System;
using System.Collections.Generic;

namespace Utilities.Process;

public sealed class ProcessSpec
{
    private readonly Dictionary<string, string?> _environmentVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> _acceptableExitCodes = new() { 0 };

    public required string FileName
    {
        get;
        init;
    }

    public string Arguments
    {
        get;
        init;
    } = string.Empty;

    public string? WorkingDirectory
    {
        get;
        init;
    }

    public IReadOnlyDictionary<string, string?> EnvironmentVariables => _environmentVariables;
    public ISet<int> AcceptableExitCodes => _acceptableExitCodes;

    public bool RedirectStandardInput
    {
        get;
        init;
    }

    public TerminationMethod TerminationMethod
    {
        get;
        init;
    } = TerminationMethod.Kill;

    public TimeSpan? ExitTimeout
    {
        get;
        init;
    }

    public string? ReadySignal
    {
        get;
        init;
    }

    public ProcessOutputStream ReadySignalStream
    {
        get;
        init;
    } = ProcessOutputStream.StandardOutput;

    public TimeSpan ReadySignalTimeout
    {
        get;
        init;
    } = TimeSpan.FromSeconds(30);

    public bool WaitForReadySignalOnStart
    {
        get;
        init;
    } = true;

    public Action<ProcessOutput>? OutputObserver
    {
        get;
        init;
    }

    public ProcessSpec WithEnvironmentVariable(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));
        _environmentVariables[key] = value;
        return this;
    }

    public ProcessSpec AllowExitCode(int exitCode)
    {
        _acceptableExitCodes.Add(exitCode);
        return this;
    }

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrEmpty(FileName, nameof(FileName));
        if (ReadySignal is not null && ReadySignalTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ReadySignalTimeout), ReadySignalTimeout, "Timeout must be positive.");
        }

        if (ExitTimeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ExitTimeout), timeout, "Timeout must be positive.");
        }

        if (_acceptableExitCodes.Count == 0)
        {
            throw new InvalidOperationException("ProcessSpec must contain at least one acceptable exit code.");
        }
    }
}
