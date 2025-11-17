using System;
using System.Threading;
using System.Threading.Tasks;

namespace Utilities.Process;

public interface IProcessHandle : IAsyncDisposable
{
    int ProcessId
    {
        get;
    }

    bool HasExited
    {
        get;
    }

    Task<bool> WaitForReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    Task<ProcessResult> WaitForExitAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    Task<ProcessResult> StopAsync(TerminationMethod? terminationMethod = null, CancellationToken cancellationToken = default);

    Task SendStandardInputAsync(string value, CancellationToken cancellationToken = default);
}
