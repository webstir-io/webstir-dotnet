using System;

namespace Utilities.Process;

public sealed class ProcessResult
{
    public int ExitCode
    {
        get;
        init;
    }

    public string StandardOutput
    {
        get;
        init;
    } = string.Empty;

    public string StandardError
    {
        get;
        init;
    } = string.Empty;

    public TimeSpan Duration
    {
        get;
        init;
    }

    public bool TimedOut
    {
        get;
        init;
    }

    public bool WasCancelled
    {
        get;
        init;
    }

    public bool IsExitCodeAccepted
    {
        get;
        init;
    }

    public bool ReadySignalReceived
    {
        get;
        init;
    }

    public bool ReadySignalTimedOut
    {
        get;
        init;
    }

    public bool CompletedSuccessfully => !TimedOut && !WasCancelled && IsExitCodeAccepted;
}
